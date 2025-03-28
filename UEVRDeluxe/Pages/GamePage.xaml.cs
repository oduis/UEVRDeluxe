#region Usings
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UEVRDeluxe.Code;
using UEVRDeluxe.Common;
using UEVRDeluxe.ViewModels;
using Windows.Devices.Enumeration;
using Windows.Media.Devices;
using Windows.System;
#endregion

namespace UEVRDeluxe.Pages;

public sealed partial class GamePage : Page {
	readonly GamePageVM VM = new();
	VoiceCommandRecognizer speechRecognizer = new();

	#region * Init
	public GamePage() {
		this.InitializeComponent();
		this.Loaded += Page_Loaded;

		// Initialize the DispatcherTimer
		hotKeyCheckTimer = new();
		hotKeyCheckTimer.Interval = TimeSpan.FromMilliseconds(400);
		hotKeyCheckTimer.Tick += HotKeyCheckTimer_Tick;
	}

	protected override void OnNavigatedTo(NavigationEventArgs e) {
		base.OnNavigatedTo(e);

		VM.GameInstallation = e.Parameter as GameInstallation;
	}

	protected override void OnNavigatingFrom(NavigatingCancelEventArgs e) {
		base.OnNavigatingFrom(e);

		Logger.Log.LogTrace("GamePage Timer stopped");
		hotKeyCheckTimer?.Stop();

		speechRecognizer?.Stop();

		MediaDevice.DefaultAudioCaptureDeviceChanged -= MediaDevice_DefaultAudioCaptureDeviceChanged;
	}

	async void Page_Loaded(object sender, RoutedEventArgs e) {
		try {
			VM.IsLoading = true;

			Logger.Log.LogTrace($"Opening games page {VM?.GameInstallation?.Name}");

			VM.LocalProfile = LocalProfile.FromUnrealVRProfile(VM.GameInstallation.EXEName);

			await PageHelpers.RefreshDescriptionAsync(webViewDescription, VM.LocalProfile?.DescriptionMD);

			VM.CurrentOpenXRRuntime = OpenXRManager.GetAllRuntimes()?.FirstOrDefault(r => r.IsDefault)?.Name ?? "( undefined )";

			hotKeyCheckTimer.Start();

			MediaDevice.DefaultAudioCaptureDeviceChanged += MediaDevice_DefaultAudioCaptureDeviceChanged;
			string defaultAudioInputDeviceId = MediaDevice.GetDefaultAudioCaptureId(AudioDeviceRole.Default);
			DisplayAudioDevice(defaultAudioInputDeviceId);

			VM.IsLoading = false;
		} catch (Exception ex) {
			await VM.HandleExceptionAsync(this.XamlRoot, ex, "Load profile error");
		}

		// WinUI selects links otherwise
		if (VM.LocalProfile == null)
			btnEdit.Focus(FocusState.Programmatic);
		else
			btnLaunch.Focus(FocusState.Programmatic);  // WInUI selects links otherwise
	}
	#endregion

	#region Search
	async void Search_Click(object sender, RoutedEventArgs e) {
		try {
			VM.IsLoading = true;

			var profileMetas = new ObservableCollection<ProfileMeta>(await AzureManager.SearchProfilesAsync(VM.GameInstallation.EXEName));
			if (profileMetas.Count == 0) {
				VM.SearchEnabled = false;  // So he does not hit again, causing costs
				throw new Exception("No profiles found in our database, or profile is not compatible with the store provider. You may try to build one yourself.");
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
			shouldStop = false;
			VM.IsRunning = true; hotKeyCheckTimer?.Stop();

			#region Launch process and wait
			var gameProcess = Injector.FindInjectableProcess(VM.GameInstallation.EXEName);

			if (gameProcess == null) {
				VM.StatusMessage = "Game not started yet. Launching via Steam...";

				Process.Start(new ProcessStartInfo { FileName = VM.GameInstallation.ShellLaunchPath, UseShellExecute = true });

				if (VM.LocalProfile?.Meta?.LateInjection == true) {
					VM.StatusMessage = "Manual injection needed";
					return;
				}

				VM.StatusMessage = "Waiting for launched game to start...";

				int delayBeforeInjectionSec = AppUserSettings.DEFAULT_DELAY_BEFORE_INJECTION_SEC;
				string appSetting = AppUserSettings.Read("DelayBeforeInjectionSec");
				if (int.TryParse(appSetting, out int iAppSetting) && iAppSetting > 0) delayBeforeInjectionSec = iAppSetting;

				DateTime? runningSinceUtc = null;
				do {
					gameProcess = Injector.FindInjectableProcess(VM.GameInstallation.EXEName);
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

			VM.StatusMessage = "Focussing game window...";
			Win32.SwitchToThisWindow(gameProcess.MainWindowHandle, true);
			gameProcess.WaitForInputIdle(100);

			#region Nullify plugins
			if (VM.LocalProfile.Meta.NullifyPlugins) {
				VM.StatusMessage = "Nullifying VR plugins...";

				IntPtr nullifierBase = Injector.InjectDllFindBase(gameProcess.Id, "UEVRPluginNullifier.dll");
				if (nullifierBase.ToInt64() < 0) throw new Exception("Failed to inject nullifier DLL");

				Injector.CallFunctionNoArgs(gameProcess.Id, "UEVRPluginNullifier.dll", nullifierBase, "nullify");
			}
			#endregion

			VM.StatusMessage = "Injecting protocol DLL...";
			Injector.InjectDll(gameProcess.Id, (VM.LinkProtocol_XR ? "openxr_loader.dll" : "openvr_api.dll"));

			VM.StatusMessage = "Injecting backend DLL...";
			Injector.InjectDll(gameProcess.Id, "UEVRBackend.dll");

			if (VM.EnableVoiceCommands) {
				VM.StatusMessage = "Starting voice recognition...";
				speechRecognizer.Start(VM.GameInstallation.EXEName);
			}

			VM.StatusMessage = "Game is running! You may see a black screen while the intro movies are playing. The UEVR in-game window will open. Press 'Ins' on keyboard or both controller joysticks to close it.";

			while (!shouldStop) {
				await Task.Delay(800);

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

				if (!gameProcess.WaitForExit(3000)) {
					VM.StatusMessage = "Game did not close properly. Killing it...";
					gameProcess.Kill();
				}
			}

			VM.StatusMessage = "Game stopped";
		} catch (Exception ex) {
			VM.StatusMessage += $" - {ex.Message}";

			await new ContentDialog {
				Title = "UEVR", Content = ex.Message, CloseButtonText = "OK", XamlRoot = this.XamlRoot
			}.ShowAsync();
		} finally {
			speechRecognizer?.Stop(); hotKeyCheckTimer?.Start();
			VM.IsRunning = false;
		}

		// So the next game may be started
		if (wasCalledViaHotKey.HasValue && wasCalledViaHotKey.Value) Frame.GoBack();
	}
	#endregion

	#region Stop
	void Stop_Click(object sender, RoutedEventArgs e) {
		VM.StatusMessage = "Stopping game...";
		shouldStop = true;
	}
	#endregion

	#region * HotKey
	readonly DispatcherTimer hotKeyCheckTimer;

	/// <summary>The first tick decides if a hotkey was passed through</summary>
	bool? wasCalledViaHotKey = null;

	void HotKeyCheckTimer_Tick(object sender, object e) {
		if (MainWindow.HotkeyEvent.IsSet) {
			MainWindow.HotkeyEvent.Reset();

			if (!VM.IsRunning && VM.LocalProfile != null) {
				if (!wasCalledViaHotKey.HasValue) wasCalledViaHotKey = true;
				Launch_Click(this, null);
			}
		} else if (!wasCalledViaHotKey.HasValue) {
			wasCalledViaHotKey = false;
		}
	}
	#endregion

	#region * Voice commands
	void NavigateVoiceCommandsPage(object sender, RoutedEventArgs e)
		=> Frame.Navigate(typeof(EditVoiceCommandsPage), VM.GameInstallation.EXEName, new DrillInNavigationTransitionInfo());

	async void OpenWinAudio_Click(object sender, RoutedEventArgs e) {
		await Launcher.LaunchUriAsync(new Uri("ms-settings:sound"));
	}

	void MediaDevice_DefaultAudioCaptureDeviceChanged(object sender, DefaultAudioCaptureDeviceChangedEventArgs args)
		=> DispatcherQueue.TryEnqueue(() => DisplayAudioDevice(args.Id));

	void DisplayAudioDevice(string deviceID) {
		if (!string.IsNullOrEmpty(deviceID)) {
			var deviceInformation = DeviceInformation.CreateFromIdAsync(deviceID).AsTask().Result;
			VM.DefaultInputDeviceName = deviceInformation?.Name ?? "Unknown Device";

			VM.EnableVoiceCommands = File.Exists(VoiceCommandProfile.GetFilePath(VM.GameInstallation.EXEName));
		} else {
			VM.DefaultInputDeviceName = "( no default audio input )";
			VM.EnableVoiceCommands = false;
		}
	}
	#endregion

	void Edit_Click(object sender, RoutedEventArgs e)
		=> Frame.Navigate(typeof(EditProfilePage), VM.GameInstallation, new DrillInNavigationTransitionInfo());

	void NavigateSettingsPage(object sender, RoutedEventArgs e)
		=> Frame.Navigate(typeof(SettingsPage), null, new DrillInNavigationTransitionInfo());

	void Back_Click(object sender, RoutedEventArgs e) { if (!VM.IsRunning) Frame.GoBack(); }
}

