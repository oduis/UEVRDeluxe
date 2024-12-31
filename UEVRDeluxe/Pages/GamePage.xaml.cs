#region Usings
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
	const string STEAMVR_EXE = "vrmonitor";
	const string VIRTUALDESKTOP_EXE = "VirtualDesktop.Streamer";

	GamePageVM VM = new();

	#region * Init
	public GamePage() {
		this.InitializeComponent();
		this.Loaded += GamePage_Loaded;
	}

	protected override void OnNavigatedTo(NavigationEventArgs e) {
		base.OnNavigatedTo(e);

		VM.GameInstallation = e.Parameter as GameInstallation;
	}

	async void GamePage_Loaded(object sender, RoutedEventArgs e) {
		try {
			VM.LocalProfile = LocalProfile.FromUnrealVRProfile(VM.GameInstallation.EXEName);

			await PageHelpers.RefreshDescriptionAsync(webViewDescription, VM.LocalProfile?.DescriptionMD);
		} catch (Exception ex) {
			await HandleExceptionAsync(ex, "Load profile error");
		}
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
			await HandleExceptionAsync(ex, "Search online error");
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

				Process.Start(new ProcessStartInfo { FileName = $"steam://rungameid/{VM.GameInstallation.SteamID}", UseShellExecute = true });

				VM.StatusMessage = "Waiting for launched game to start...";
				do {
					gameProcess = Process.GetProcessesByName(VM.GameInstallation.EXEName.ToLowerInvariant()).FirstOrDefault();
					await Task.Delay(1000);  // not if, since we want to give the EXE a second time...

					if (shouldStop) return;  // if the user cancelled
				} while (gameProcess == null);
			} else VM.StatusMessage = "Game already running, injecting...";
			#endregion

			const string SUBFOLDER = "UEVR\\";

			#region Nullify plugins
			if (VM.LocalProfile.Meta.NullifyPlugins) {
				VM.StatusMessage = "Nullifying VR plugins...";

				IntPtr nullifierBase;
				if (Injector.InjectDll(gameProcess.Id, SUBFOLDER + "UEVRPluginNullifier.dll", out nullifierBase) && nullifierBase.ToInt64() > 0) {
					if (!Injector.CallFunctionNoArgs(gameProcess.Id, "UEVRPluginNullifier.dll", nullifierBase, "nullify", true)) {
						Debug.WriteLine("Failed to nullify VR plugins.");
					}
				} else {
					Debug.WriteLine("Failed to nullify VR plugins.");
				}
			}
			#endregion

			VM.StatusMessage = "Injecting protocol DLL...";
			Injector.InjectDll(gameProcess.Id, SUBFOLDER + (VM.LinkProtocol_XR ? "openxr_loader.dll" : "openvr_api.dll"));

			VM.StatusMessage = "Injecting backend DLL...";
			Injector.InjectDll(gameProcess.Id, SUBFOLDER + "UEVRBackend.dll");

			VM.StatusMessage = "Focussing game window...";
			Injector.SwitchToThisWindow(gameProcess.MainWindowHandle, true);

			VM.StatusMessage = "Game is running! Press 'Ins' on keyboard or both controller joysticks to close the UEVR window in game.";

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

	void Edit_Click(object sender, RoutedEventArgs e) => Frame.Navigate(typeof(EditProfilePage), VM.GameInstallation, new DrillInNavigationTransitionInfo());

	void Back_Click(object sender, RoutedEventArgs e) { if (!VM.IsRunning) Frame.GoBack(); }

	async Task HandleExceptionAsync(Exception ex, string title) {
		VM.IsLoading = false;

		await new ContentDialog {
			Title = title, CloseButtonText = "OK", XamlRoot = this.XamlRoot,
			Content = string.IsNullOrEmpty(ex.Message) ? ex.ToString() : ex.Message
		}.ShowAsync();
	}
}

