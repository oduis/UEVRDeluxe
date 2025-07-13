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
using System.Text;
using System.Threading.Tasks;
using UEVRDeluxe.Code;
using UEVRDeluxe.Common;
using UEVRDeluxe.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.Devices.Enumeration;
using Windows.Media.Devices;
using Windows.System;
#endregion

namespace UEVRDeluxe.Pages;

public sealed partial class GamePage : Page {
	readonly GamePageVM VM = new();
	VoiceCommandRecognizer speechRecognizer;

	const string KEY_ENABLE_VOICE_COMMANDS = "EnableVoiceCommands";

	#region * Init
	public GamePage() {
		this.InitializeComponent();
		this.Loaded += Page_Loaded;

		// Initialize the DispatcherTimer
		hotKeyCheckTimer = new() { Interval = TimeSpan.FromMilliseconds(400) };
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
		speechRecognizer = null;

		MediaDevice.DefaultAudioCaptureDeviceChanged -= MediaDevice_DefaultAudioCaptureDeviceChanged;
	}

	async void Page_Loaded(object sender, RoutedEventArgs e) {
		try {
			VM.IsLoading = true;

			Logger.Log.LogTrace($"Opening games page {VM?.GameInstallation?.Name}");

			VM.LocalProfile = LocalProfile.FromUnrealVRProfile(VM.GameInstallation.EXEName);

			VM.CurrentUEVRNightlyNumber = Injector.GetInstalledUEVRNightlyNumber();

			await PageHelpers.RefreshDescriptionAsync(webViewDescription, VM.LocalProfile?.DescriptionMD, ActualTheme == ElementTheme.Dark);

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

			var profileMetas = new ObservableCollection<ProfileMeta>(await AzureManager.SearchProfilesAsync(VM.GameInstallation.EXEName, true));
			if (profileMetas.Count == 0) {
				VM.SearchEnabled = false;  // So he does not hit again, causing costs
				throw new Exception("No compatible profiles found in our database. "
					+ "Just press 'Create Profile' to build one yourself.");
			}

			Frame.Navigate(typeof(DownloadProfilePage), new DownloadProfilePageArgs {
				OriginalEXEName = VM.GameInstallation.EXEName, ProfileMetas = profileMetas
			}, new DrillInNavigationTransitionInfo());

			VM.IsLoading = false;
		} catch (Exception ex) {
			await VM.HandleExceptionAsync(this.XamlRoot, ex, "Search online error");
		}
	}
	#endregion

	#region Launch
	bool shouldStop;

	void OnInjectRequested() => Launch_Click(this, null);

	async void Launch_Click(object sender, RoutedEventArgs e) {
		try {
			if (VM.EnableVoiceCommands && Win32.IsUserAnAdmin())
				throw new Exception("Start UEVR Easy without administrator privileges to use voice commands");

			shouldStop = false;
			VM.IsRunning = true; hotKeyCheckTimer?.Stop();

			#region Setup voice recognition
			if (VM.EnableVoiceCommands) {
				// might already be started by the first launch on late injection
				if (speechRecognizer == null) {
					VM.StatusMessage = "Starting voice recognition...";
					speechRecognizer = new();
					speechRecognizer.InjectRequested += OnInjectRequested;
					speechRecognizer.Start(VM.GameInstallation.EXEName);
				}
			} else {
				speechRecognizer?.Stop();
				speechRecognizer = null;
			}

			AppUserSettings.Write(KEY_ENABLE_VOICE_COMMANDS, VM.EnableVoiceCommands.ToString());
			#endregion

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

				int delayBeforeInjectionSec = AppUserSettings.GetDelayBeforeInjectionSec();

				DateTime? runningSinceUtc = null;
				do {
					gameProcess = Injector.FindInjectableProcess(VM.GameInstallation.EXEName);
					if (gameProcess != null) {
						if (runningSinceUtc == null) {
							VM.StatusMessage = $"Game started. Waiting {delayBeforeInjectionSec} seconds before injection.";
							runningSinceUtc = DateTime.UtcNow;
						}
					} else runningSinceUtc = null;  // In case it crashed on start or something

					await Task.Delay(300);

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

			// Stop voice commands
			if (speechRecognizer != null) {
				speechRecognizer.InjectRequested -= OnInjectRequested;

				if (speechRecognizer.StopAfterInjected) {
					VM.StatusMessage += "Stopping voice commands after injecting";
					speechRecognizer.Stop(); speechRecognizer = null;
				}
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

			speechRecognizer?.Stop(); speechRecognizer = null;

			VM.StatusMessage = "Game stopped";
		} catch (Exception ex) {
			VM.StatusMessage += $" - {ex.Message}";

			await new ContentDialog {
				Title = "UEVR", Content = ex.Message, CloseButtonText = "OK", XamlRoot = this.XamlRoot
			}.ShowAsync();

			speechRecognizer?.Stop(); speechRecognizer = null;
		} finally {
			hotKeyCheckTimer?.Start();
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

	#region * HotKey/Timer
	readonly DispatcherTimer hotKeyCheckTimer;

	/// <summary>The first tick decides if a hotkey was passed through</summary>
	bool? wasCalledViaHotKey = null;

	void HotKeyCheckTimer_Tick(object sender, object e) {
		if (MainWindow.HotkeyEvent.IsSet) {
			MainWindow.HotkeyEvent.Reset();

			if (!VM.IsRunning && VM.LocalProfile != null) {
				if (!wasCalledViaHotKey.HasValue) wasCalledViaHotKey = true;
				OnInjectRequested();
			}
		} else if (!wasCalledViaHotKey.HasValue) {
			wasCalledViaHotKey = false;
		} else {
			var gameProcess = Injector.FindInjectableProcess(VM.GameInstallation.EXEName);
			VM.IsGameProcessRunning = gameProcess != null && !gameProcess.HasExited;
		}
	}
	#endregion

	#region * Voice commands
	async void NavigateVoiceCommandsPage(object sender, RoutedEventArgs e) {
		if (Win32.IsUserAnAdmin()) {
			await new ContentDialog {
				Title = "UEVR", Content = "Start UEVR Easy without administrator privileges to use voice commands",
				CloseButtonText = "OK", XamlRoot = this.XamlRoot
			}.ShowAsync();
			return;
		}

		Frame.Navigate(typeof(EditVoiceCommandsPage), VM.GameInstallation.EXEName, new DrillInNavigationTransitionInfo());
	}
	async void OpenWinAudio_Click(object sender, RoutedEventArgs e) {
		await Launcher.LaunchUriAsync(new Uri("ms-settings:sound"));
	}

	void MediaDevice_DefaultAudioCaptureDeviceChanged(object sender, DefaultAudioCaptureDeviceChangedEventArgs args)
		=> DispatcherQueue.TryEnqueue(() => DisplayAudioDevice(args.Id));

	void DisplayAudioDevice(string deviceID) {
		if (!string.IsNullOrEmpty(deviceID)) {
			var deviceInformation = DeviceInformation.CreateFromIdAsync(deviceID).AsTask().Result;
			VM.DefaultInputDeviceName = deviceInformation?.Name ?? "Unknown Device";

			VM.EnableVoiceCommands = bool.Parse(AppUserSettings.Read(KEY_ENABLE_VOICE_COMMANDS) ?? true.ToString())
				&& File.Exists(VoiceCommandProfile.GetFilePath(VM.GameInstallation.EXEName))
				&& !Win32.IsUserAnAdmin();
		} else {
			VM.DefaultInputDeviceName = "( no default audio input )";
			VM.EnableVoiceCommands = false;
		}
	}
	#endregion

	#region CopySupportInfo
	async void CopySupportInfo_Click(object sender, RoutedEventArgs args) {
		var sbInfo = new StringBuilder();
		sbInfo.AppendLine($"Game: {VM.GameInstallation.Name} [{VM.GameInstallation.EXEName}]");
		sbInfo.Append($"Profile: ");

		if (VM.LocalProfile?.Meta == null) {
			sbInfo.AppendLine("( Free profile )");
		} else {
			sbInfo.AppendLine(
				($"{VM.LocalProfile.Meta.ModifiedDate:yyyy-MM-dd} by {VM.LocalProfile.Meta.AuthorName}: {VM.LocalProfile.Meta.Remarks}").TrimEnd([' ', ':']));
		}

		DateTime? lastModified = VM.LocalProfile.GetConfigFileLastModified();
		if (lastModified.HasValue) sbInfo.AppendLine($"Config.txt last modified: {lastModified.Value.ToString("yyyy-MM-dd")}");

		sbInfo.AppendLine($"UEVR Backend version: {Injector.GetInstalledUEVRNightlyNumber()}");
		sbInfo.AppendLine("HW Scheduling: " + (SystemInfo.IsHardwareSchedulingEnabled() ? "Enabled" : "Disabled"));

		var installedGPUs = SystemInfo.GetInstalledGPUs();
		sbInfo.AppendLine("GPUs: " + string.Join(", ", installedGPUs));

		sbInfo.AppendLine("OpenXR Toolkit: " + (OpenXRManager.IsOpenXRToolkitEnabled() ? "Enabled" : "Disabled"));

		sbInfo.AppendLine("Installed OpenXR Runtimes:");
		var runtimes = OpenXRManager.GetAllRuntimes();
		foreach (var runtime in runtimes.OrderByDescending(r => r.IsDefault))
			sbInfo.AppendLine($"- {runtime.Name}{(runtime.IsDefault ? " [DEFAULT]" : "")}");

		var dp = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
		dp.SetText(sbInfo.ToString());
		Clipboard.SetContent(dp);

		await new ContentDialog {
			Title = "UEVR", Content = "Support info copied to clipboard. Paste it on Discord Flatscreen 2 VR Modding Community along with your question.", CloseButtonText = "OK", XamlRoot = this.XamlRoot
		}.ShowAsync();
	}
	#endregion

	void Edit_Click(object sender, RoutedEventArgs e)
		=> Frame.Navigate(typeof(EditProfilePage), VM.GameInstallation, new DrillInNavigationTransitionInfo());

	void NavigateSettingsPage(object sender, RoutedEventArgs e)
		=> Frame.Navigate(typeof(SettingsPage), null, new DrillInNavigationTransitionInfo());

	void Back_Click(object sender, RoutedEventArgs e) { if (!VM.IsRunning) Frame.GoBack(); }
}

