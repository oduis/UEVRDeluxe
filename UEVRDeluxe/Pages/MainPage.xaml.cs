#region Usings
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Threading.Tasks;
using UEVRDeluxe.Code;
using UEVRDeluxe.Common;
using UEVRDeluxe.ViewModels;
#endregion

namespace UEVRDeluxe.Pages;

public sealed partial class MainPage : Page {
	readonly MainPageVM VM = new();

	#region * Init
	public MainPage() {
		this.InitializeComponent();

		// Initialize the DispatcherTimer
		hotKeyCheckTimer = new();
		hotKeyCheckTimer.Interval = TimeSpan.FromMilliseconds(300); // Adjust the interval as needed
		hotKeyCheckTimer.Tick += HotKeyCheckTimer_Tick;
	}

	void GameIcon_ImageFailed(object sender, ExceptionRoutedEventArgs e) {
		var image = (Image)sender;
		image.Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri("ms-appx:///Assets/GenericGameLogo.jpg"));
	}

	protected override void OnNavigatingFrom(NavigatingCancelEventArgs e) {
		base.OnNavigatingFrom(e);

		Logger.Log.LogTrace("MainPage Timer stopped");
		hotKeyCheckTimer?.Stop();
	}

	async void Page_Loaded(object sender, RoutedEventArgs e) {
		try {
			VM.IsLoading = true; VM.PleaseWaitVisible = Visibility.Visible;
			await Task.Delay(60);  // Give the UI a chance to update

			await CheckVersionAsync();

			InitSort();

			VM.Games = new System.Collections.ObjectModel.ObservableCollection<GameInstallation>(
				await GameStoreManager.FindAllUEVRGamesAsync(false));
			VM.PleaseWaitVisible = Visibility.Collapsed;
			SortGames();

			// Auto-Launch game?
			string launchId = App.ConsumeLaunchGameId();
			if (!string.IsNullOrWhiteSpace(launchId)) {
				var gameToLaunch = VM.Games?.FirstOrDefault(g => g.ID==launchId);

				if (gameToLaunch != null) {
					Logger.Log.LogInformation($"Auto-launching game {gameToLaunch.Name} ({launchId}) from command line");

					VM.IsLoading = false;
					Frame.Navigate(typeof(GamePage), new GameNavigationArgs { Game = gameToLaunch, AutoLaunch = true });

					return;
				} else {
					Logger.Log.LogWarning($"Launch argument game not found: {launchId}");
				}
			}

			// Check if hardware scheduling is enabled and warn the user
			var warnings = new List<string>();

			if (SystemInfo.IsHardwareSchedulingEnabled())
				warnings.Add("Consider disabling 'Hardware Accelerated GPU Scheduling' in your Windows 'Graphics settings', only if you experience issues in games");

			if (OpenXRManager.IsOpenXRToolkitEnabled())
				warnings.Add("OpenXR Toolkit is active. Uninstall or disable it if you experience issues.");

			if (warnings.Any()) VM.Warning = string.Join("\n", warnings);

			hotKeyCheckTimer.Start();

			await RefreshUpdateButtonLabelAsync();
		} catch (Exception ex) {
			await VM.HandleExceptionAsync(this.XamlRoot, ex, "Startup");
		} finally {
			VM.PleaseWaitVisible = Visibility.Collapsed;
		}

		VM.IsLoading = false;
		gvGames.Focus(FocusState.Programmatic);  // WinUI selects links otherwise
	}
	#endregion

	#region * Link Handler
	void NavigateAdminPage(object sender, RoutedEventArgs e)
		=> Frame.Navigate(typeof(AdminPage), null, new DrillInNavigationTransitionInfo());

	void NavigateAllProfilesPage(object sender, RoutedEventArgs e)
		=> Frame.Navigate(typeof(AllProfilesPage), null, new DrillInNavigationTransitionInfo());

	void NavigateSettingsPage(object sender, RoutedEventArgs e)
		=> Frame.Navigate(typeof(SettingsPage), null, new DrillInNavigationTransitionInfo());

	void GamesView_ItemClick(object sender, ItemClickEventArgs e)
		=> Frame.Navigate(typeof(GamePage), e.ClickedItem, new SlideNavigationTransitionInfo { Effect = SlideNavigationTransitionEffect.FromRight });
	#endregion

	#region CheckVersionAsync
	async Task CheckVersionAsync() {
		const double DAYS_BETWEEN_CHECKS = 1.0;
		const string KEY_LAST_UPDATE_CHECK = "LastUpdateCheckUTC";
		const string FORMAT_LAST_UPDATE_CHECK = "yyyyMMddHHmm";

		if (string.IsNullOrWhiteSpace(CompiledSecret.AUTOUPDATE_URL)) return;  // disabled e.g. for DEBUG

		try {
			// Get Settings from config
			DateTime? lastUpdateCheckUtc = null;

			string settingsVal = AppUserSettings.Read(KEY_LAST_UPDATE_CHECK);

			if (!string.IsNullOrEmpty(settingsVal)) {
				lastUpdateCheckUtc = DateTime.ParseExact(settingsVal, FORMAT_LAST_UPDATE_CHECK, CultureInfo.InvariantCulture);
			}

			var utcNow = DateTime.UtcNow;
			if (!lastUpdateCheckUtc.HasValue || utcNow.Subtract(lastUpdateCheckUtc.Value).TotalDays > DAYS_BETWEEN_CHECKS) {
				Logger.Log.LogInformation("Starting version check");

				// Short timeout, since the user will thing the app hangs otherwise
				using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(4) }) {
					var autoupdate = await client.GetFromJsonAsync<Autoupdate>(CompiledSecret.AUTOUPDATE_URL);
					if (autoupdate != null) {
						AppUserSettings.Write(KEY_LAST_UPDATE_CHECK, utcNow.ToString(FORMAT_LAST_UPDATE_CHECK));

						if (new Version(autoupdate.Version).CompareTo(Assembly.GetExecutingAssembly().GetName().Version) > 0) {
							var dialogResult = await new ContentDialog {
								Title = "Update available",
								PrimaryButtonText = "Yes",
								CloseButtonText = "No",
								XamlRoot = this.XamlRoot,
								Content = ($"An update ot UEVR Easy Injector is available. Would you like to download the update now?\n{autoupdate.ReleaseNotes}").Trim()
							}.ShowAsync();

							if (dialogResult == ContentDialogResult.Primary)
								await Windows.System.Launcher.LaunchUriAsync(new Uri(autoupdate.WebURL));
						}
					}
				}
			}
		} catch (Exception ex) {
			// Gracefully ignore
			Logger.Log.LogCritical(ex, "Cannot check version");
		}
	}
	#endregion

	#region Rescan
	async void Rescan_Click(object sender, RoutedEventArgs e) {
		try {
			VM.IsLoading = true;
			VM.PleaseWaitVisible = Visibility.Visible;
			await Task.Delay(60);  // Give the UI a chance to update

			InitSort();
			VM.Games.Clear();
			var games = await GameStoreManager.FindAllUEVRGamesAsync(true);
			foreach (var game in games) VM.Games.Add(game);
			VM.PleaseWaitVisible = Visibility.Collapsed;
			SortGames();

			await new ContentDialog {
				Title = "Rescan finished", CloseButtonText = "OK", XamlRoot = this.XamlRoot,
				Content = "Newly installed Steam game missing? Shut down Steam, restart it, and rescan. Alternatively, you can reboot your system, start Steam, and then start UEVR Easy."
			}.ShowAsync();
		} catch (Exception ex) {
			await VM.HandleExceptionAsync(this.XamlRoot, ex, "Rescan");
		} finally {
			VM.PleaseWaitVisible = Visibility.Collapsed;
		}

		VM.IsLoading = false;
	}
	#endregion

	#region * Sorting
	static string sortBy = "recent";  // static so it survives back and forth

	void InitSort() {
		if (sortBy == "recent") rbSortRecent.IsChecked = true;
		else rbSortName.IsChecked = true;
	}
	void SortRecentChecked(object sender, RoutedEventArgs e) { sortBy = "recent"; SortGames(); }
	void SortNameChecked(object sender, RoutedEventArgs e) { sortBy = "name"; SortGames(); }

	void SortGames() {
		if (VM.Games == null) return;

		Debug.WriteLine("Sort by " + sortBy);

		// build a reference list. We only need it for sorting
		List<GameInstallation> sortedList;
		if (sortBy == "recent")
			sortedList = VM.Games.OrderByDescending(g => g.LastPlayed ?? new DateTime()).ThenBy(g => g.Name).ToList();
		else
			sortedList = VM.Games.OrderBy(g => g.Name).ToList();

		// Do NOT recreated the ObservableCollection
		for (int i = 0; i < sortedList.Count; i++) VM.Games.Move(VM.Games.IndexOf(sortedList[i]), i);
	}
	#endregion

	#region * HotKey for quicklaunch
	DispatcherTimer hotKeyCheckTimer;

	void HotKeyCheckTimer_Tick(object sender, object e) {
		if (MainWindow.HotkeyEvent.IsSet) {
			MainWindow.HotkeyEvent.Reset();
			Logger.Log.LogDebug("Hotkey pressed");

			// Try to the the EXE of the foreground window (presuably the game)
			IntPtr hWnd = Win32.GetForegroundWindow();
			if (hWnd == IntPtr.Zero) {
				Logger.Log.LogWarning("No foreground window");
				return;
			}

			Win32.GetWindowThreadProcessId(hWnd, out uint processId);
			IntPtr hProcess = Win32.OpenProcess(ProcessAccessFlags.QUERY_INFORMATION | ProcessAccessFlags.VM_READ, false, processId);
			if (hProcess == IntPtr.Zero) {
				Logger.Log.LogWarning("Cannot find process");
				return;
			}

			char[] buffer = new char[512];
			Win32.GetModuleBaseName(hProcess, IntPtr.Zero, buffer, buffer.Length);

			Win32.CloseHandle(hProcess);

			string exeName = new string(buffer).TrimEnd('\0');
			if (string.IsNullOrEmpty(exeName)) {
				Logger.Log.LogWarning("Cannot find EXE name");
				return;
			}

			exeName = Path.GetFileNameWithoutExtension(exeName);
			Logger.Log.LogDebug($"Foreground EXE: {exeName}");

			var foundGames = VM.Games.Where(g => string.Equals(g.EXEName, exeName, StringComparison.OrdinalIgnoreCase));
			if (foundGames.Count() != 1) {
				Logger.Log.LogWarning($"{foundGames.Count()} games found, nothing to call");
				return;
			}

			hotKeyCheckTimer.Stop(); // so it doesnt pick the hotkey up
			MainWindow.HotkeyEvent.Set();  // so the next page will pick it up
			Frame.Navigate(typeof(GamePage), foundGames.First(), new SlideNavigationTransitionInfo { Effect = SlideNavigationTransitionEffect.FromRight });
		}
	}
	#endregion

	#region UpdateUEVR
	async Task RefreshUpdateButtonLabelAsync() {
		int? currentNightlyNumber = Injector.GetInstalledUEVRNightlyNumber();

		int? latestNightlyNumber;
		try {
			latestNightlyNumber = await Injector.ReadLatestUEVRNightlyNumberAsync();
		} catch (Exception ex) {
			Logger.Log.LogError(ex, "Failed to read latest UEVR nightly number");
			latestNightlyNumber = null;
		}

		if (latestNightlyNumber.HasValue && currentNightlyNumber.HasValue)
			if (latestNightlyNumber == currentNightlyNumber) {
				VM.DownloadButtonLabel = $"Change UEVR version ({latestNightlyNumber} [latest] installed)";
			} else {
				VM.DownloadButtonLabel = $"Upgrade UEVR to version {latestNightlyNumber} ({currentNightlyNumber} installed)";
			}
		else if (currentNightlyNumber.HasValue) {
			VM.DownloadButtonLabel = $"Upgrade UEVR version ({currentNightlyNumber} installed)";
		} else VM.DownloadButtonLabel = "Upgrade UEVR version";
	}

	async Task<int?> ShowUpdateNightlyDialogAsync(int? installedNightlyNumber, int latestNightlyNumber) {
		bool latestInstalled = installedNightlyNumber == latestNightlyNumber;
		var radioLatest = new RadioButton {
			Content = $"Latest version ({latestNightlyNumber}{(latestInstalled ? ", already installed" : "")})", IsChecked = !latestInstalled
		};
		var radioSpecific = new RadioButton {
			Content = "Specific nightly number:", IsChecked = latestInstalled
		};

		// not in the same parent, so do it manually
		radioLatest.Checked += (object s, RoutedEventArgs e) => radioSpecific.IsChecked = false;
		radioSpecific.Checked += (s, e) => radioLatest.IsChecked = false;

		var nightlyBox = new TextBox { PlaceholderText = "e.g. 1036", Width = 120 };
		nightlyBox.TextChanged += (s, e) => { if (!string.IsNullOrEmpty(nightlyBox.Text)) radioSpecific.IsChecked = true; };

		var spSpecific = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
		spSpecific.Children.Add(radioSpecific);
		spSpecific.Children.Add(nightlyBox);

		var errorText = new TextBlock {
			Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red), Visibility = Visibility.Collapsed
		};

		var spMain = new StackPanel { Spacing = 8 };
		spMain.Children.Add(radioLatest);
		spMain.Children.Add(spSpecific);
		spMain.Children.Add(errorText);

		var dialog = new ContentDialog {
			Title = "Update UEVR Backend to", XamlRoot = this.XamlRoot,
			PrimaryButtonText = "Update", CloseButtonText = "Cancel",
			Content = spMain
		};

		int? resultNightly = null;
		dialog.PrimaryButtonClick += (s, e) => {
			if (radioLatest.IsChecked == true) {
				resultNightly = null;
			} else if (radioSpecific.IsChecked == true && int.TryParse(nightlyBox.Text, out int nightlyNumber) && nightlyNumber > 0) {
				resultNightly = nightlyNumber;
			} else {
				errorText.Text = "Please enter a valid nightly number";
				errorText.Visibility = Visibility.Visible;
				e.Cancel = true;
			}
		};

		var result = await dialog.ShowAsync();
		if (result != ContentDialogResult.Primary) return -1; // Cancelled
		return resultNightly;
	}

	async void UpdateUEVR_Click(object sender, RoutedEventArgs e) {
		try {
			VM.IsLoading = true;

			int latestNightlyNumber = await Injector.ReadLatestUEVRNightlyNumberAsync();
			int? installedNightlyNumber = Injector.GetInstalledUEVRNightlyNumber();

			int? nightlyNumber = await ShowUpdateNightlyDialogAsync(installedNightlyNumber, latestNightlyNumber);
			if (nightlyNumber == -1) { VM.IsLoading = false; return; }

			Logger.Log.LogInformation($"Starting UEVR Nightly update (nightly: {nightlyNumber?.ToString() ?? "latest"})");

			await CmdManager.UpdateBackendAsync(nightlyNumber ?? latestNightlyNumber);

			await RefreshUpdateButtonLabelAsync();

			VM.IsLoading = false;

			await new ContentDialog {
				Title = "UEVR Nightly", CloseButtonText = "OK", XamlRoot = this.XamlRoot,
				Content = "Updated successfully"
			}.ShowAsync();
		} catch (Exception ex) {
			VM.IsLoading = false;
			await VM.HandleExceptionAsync(this.XamlRoot, ex, "Download UEVR Nightly");
		}
	}
	#endregion

	bool TryHandleAutoLaunch() {
		string launchId = App.ConsumeLaunchGameId();
		if (string.IsNullOrWhiteSpace(launchId) || VM.Games == null) return false;

		var gameToLaunch = VM.Games.FirstOrDefault(g => string.Equals(g.ID, launchId, StringComparison.OrdinalIgnoreCase));
		if (gameToLaunch == null) {
			Logger.Log.LogWarning($"Launch argument game not found: {launchId}");
			return false;
		}

		Logger.Log.LogInformation($"Auto-launching game {gameToLaunch.Name} ({launchId}) from command line");

		Frame.Navigate(typeof(GamePage), new GameNavigationArgs { Game = gameToLaunch, AutoLaunch = true });
		return true;
	}
}
