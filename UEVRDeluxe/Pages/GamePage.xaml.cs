#region Usings
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using UEVRDeluxe.Code;
using UEVRDeluxe.Common;
using UEVRDeluxe.ViewModels;
#endregion

namespace UEVRDeluxe.Pages;

public sealed partial class GamePage : Page {
	GamePageVM VM = new();

	#region * Init
	public GamePage() {
		this.InitializeComponent();
		this.Loaded += GamePage_Loaded;
		this.Unloaded += GamePage_Unloaded;

		// Initialize the DispatcherTimer
		hotKeyCheckTimer = new DispatcherTimer();
		hotKeyCheckTimer.Interval = TimeSpan.FromMilliseconds(500); // Adjust the interval as needed
		hotKeyCheckTimer.Tick += HotKeyCheckTimer_Tick;
	}

	protected override void OnNavigatedTo(NavigationEventArgs e) {
		base.OnNavigatedTo(e);

		VM.GameInstallation = e.Parameter as GameInstallation;
	}

	async void GamePage_Loaded(object sender, RoutedEventArgs e) {
		try {
			Logger.Log.LogTrace($"Opening games page {VM?.GameInstallation?.Name}");

			VM.LocalProfile = LocalProfile.FromUnrealVRProfile(VM.GameInstallation.EXEName);

			await PageHelpers.RefreshDescriptionAsync(webViewDescription, VM.LocalProfile?.DescriptionMD);

			VM.CurrentOpenXRRuntime = OpenXRManager.GetAllRuntimes()?.FirstOrDefault(r => r.IsDefault)?.Name ?? "( undefinded )";

			hotKeyCheckTimer.Start();
		} catch (Exception ex) {
			await VM.HandleExceptionAsync(this.XamlRoot, ex, "Load profile error");
		}
	}

	void GamePage_Unloaded(object sender, RoutedEventArgs e) {
		Logger.Log.LogTrace("GamePage Timer stopped");
		hotKeyCheckTimer.Stop();
	}
	#endregion

	#region Search
	async void Search_Click(object sender, RoutedEventArgs e) {
		try {
			VM.IsLoading = true;

			var profileMetas = new ObservableCollection<ProfileMeta>(await AzureManager.SearchProfilesAsync(VM.GameInstallation.EXEName));
			if (profileMetas.Count == 0) {
				VM.SearchEnabled = false;  // So he does not hit again, causing costs
				throw new Exception("No profiles found in our database. You may try to build one yourself.");
			}

			Frame.Navigate(typeof(DownloadProfilePage), profileMetas, new DrillInNavigationTransitionInfo());

			VM.IsLoading = false;
		} catch (Exception ex) {
			await VM.HandleExceptionAsync(this.XamlRoot, ex, "Search online error");
		}
	}
	#endregion

	#region Launch
	bool shouldStop;

	async void Launch_Click(object sender, RoutedEventArgs e) {
		try {
			shouldStop = false; VM.IsRunning = true;

			#region Launch process and wait
			var gameProcess = Process.GetProcessesByName(VM.GameInstallation.EXEName.ToLowerInvariant()).FirstOrDefault();
			if (gameProcess == null) {
				VM.StatusMessage = "Game not started yet. Launching via Steam...";

				Process.Start(new ProcessStartInfo { FileName = VM.GameInstallation.ShellLaunchPath, UseShellExecute = true });

				if (VM.LocalProfile?.Meta?.LateInjection == true) {
					VM.StatusMessage = "Manual injection needed";
					throw new Exception("Game launched, but this game needs you to start your session manually before injecting.\nWhen in 3D game view, hit 'Start game' again or press Strg+Alt+U to inject.");
				}

				VM.StatusMessage = "Waiting for launched game to start...";

				int delayBeforeInjectionSec = AppUserSettings.DEFAULT_DELAY_BEFORE_INJECTION_SEC;
				string appSetting = AppUserSettings.Read("DelayBeforeInjectionSec");
				if (int.TryParse(appSetting, out int iAppSetting) && iAppSetting > 0) delayBeforeInjectionSec = iAppSetting;

				DateTime? runningSinceUtc = null;
				do {
					gameProcess = Process.GetProcessesByName(VM.GameInstallation.EXEName.ToLowerInvariant()).FirstOrDefault();
					if (gameProcess != null) {
						if (runningSinceUtc == null) {
							VM.StatusMessage = $"Game started. Waiting {delayBeforeInjectionSec} seconds before injection.";
							runningSinceUtc = DateTime.UtcNow;
						}
					} else runningSinceUtc = null;  // In case it crashed on start or something

					await Task.Delay(1000);

					if (shouldStop) return;  // if the user cancelled
				} while (runningSinceUtc == null || DateTime.UtcNow.Subtract(runningSinceUtc.Value).TotalSeconds < delayBeforeInjectionSec);
			} else VM.StatusMessage = "Game already running, injecting...";
			#endregion

			const string SUBFOLDER = "UEVR\\";

			#region Nullify plugins
			if (VM.LocalProfile.Meta.NullifyPlugins) {
				VM.StatusMessage = "Nullifying VR plugins...";

				IntPtr nullifierBase;
				if (Injector.InjectDll(gameProcess.Id, SUBFOLDER + "UEVRPluginNullifier.dll", out nullifierBase) && nullifierBase.ToInt64() > 0) {
					if (!Injector.CallFunctionNoArgs(gameProcess.Id, SUBFOLDER + "UEVRPluginNullifier.dll", nullifierBase, "nullify", true)) {
						Logger.Log.LogError("Failed to nullify VR plugins.");
					}
				} else {
					Logger.Log.LogError("Failed to nullify VR plugins.");
				}
			}
			#endregion

			VM.StatusMessage = "Injecting protocol DLL...";
			Injector.InjectDll(gameProcess.Id, SUBFOLDER + (VM.LinkProtocol_XR ? "openxr_loader.dll" : "openvr_api.dll"));

			VM.StatusMessage = "Injecting backend DLL...";
			Injector.InjectDll(gameProcess.Id, SUBFOLDER + "UEVRBackend.dll");

			VM.StatusMessage = "Focussing game window...";
			Win32.SwitchToThisWindow(gameProcess.MainWindowHandle, true);

			VM.StatusMessage = "Game is running! You may see a black screen while the intro movies are playing. The UEVR in-game window will open. Press 'Ins' on keyboard or both controller joysticks to close it.";

			while (!shouldStop) {
				await Task.Delay(1000);

				var data = SharedMemory.GetData();

				if (data != null) {
					if (data.Value.signalFrontendConfigSetup) {
						// UEVR sends updated to the config. We don't use it, just acknowledge it
						SharedMemory.SendCommand(SharedMemory.Command.ConfigSetupAcknowledged);
					}
				} else {
					shouldStop = true;
				}
			}

			if (!gameProcess.HasExited) {
				// if the user cancelled
				VM.StatusMessage = "Stopping game...";

				gameProcess.WaitForInputIdle(100);

				SharedMemory.SendCommand(SharedMemory.Command.Quit);

				if (!gameProcess.WaitForExit(3000)) gameProcess.Kill();
			}

			VM.StatusMessage = "Game stopped";
		} catch (Exception ex) {
			VM.StatusMessage += $" - {ex.Message}";

			await new ContentDialog {
				Title = "UEVR", Content = ex.Message, CloseButtonText = "OK", XamlRoot = this.XamlRoot
			}.ShowAsync();
		} finally {
			VM.IsRunning = false;
		}
	}
	#endregion

	#region Stop
	void Stop_Click(object sender, RoutedEventArgs e) {
		VM.StatusMessage = "Stopping game...";
		shouldStop = true;
	}
	#endregion

	#region * HotKey
	DispatcherTimer hotKeyCheckTimer;

	void HotKeyCheckTimer_Tick(object sender, object e) {
		if (MainWindow.HotkeyEvent.IsSet) {
			MainWindow.HotkeyEvent.Reset();
			if (!VM.IsRunning) Launch_Click(this, null);
		}
	}
	#endregion

	void Edit_Click(object sender, RoutedEventArgs e)
		=> Frame.Navigate(typeof(EditProfilePage), VM.GameInstallation, new DrillInNavigationTransitionInfo());

	void NavigateSettingsPage(object sender, RoutedEventArgs e)
		=> Frame.Navigate(typeof(SettingsPage), null, new DrillInNavigationTransitionInfo());

	void Back_Click(object sender, RoutedEventArgs e) { if (!VM.IsRunning) Frame.GoBack(); }
}

