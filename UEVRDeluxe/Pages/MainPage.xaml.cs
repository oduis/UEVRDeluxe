#region Usings
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Win32;
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
	const string REGKEY_GRAPHICS = @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers";
	const string REGKEY_NAME_SCHEDULER = "HwSchMode";

	readonly MainPageVM VM = new();

	#region * Init
	public MainPage() {
		this.InitializeComponent();

		// Initialize the DispatcherTimer
		hotKeyCheckTimer = new DispatcherTimer();
		hotKeyCheckTimer.Interval = TimeSpan.FromMilliseconds(300); // Adjust the interval as needed
		hotKeyCheckTimer.Tick += HotKeyCheckTimer_Tick;
	}

	protected override void OnNavigatingFrom(NavigatingCancelEventArgs e) {
		base.OnNavigatingFrom(e);

		Logger.Log.LogTrace("MainPage Timer stopped");
		hotKeyCheckTimer?.Stop();
	}

	async void Page_Loaded(object sender, RoutedEventArgs e) {
		try {
			VM.IsLoading = true;

			await CheckVersionAsync();

			InitSort();
			VM.Games = new System.Collections.ObjectModel.ObservableCollection<GameInstallation>(
				await GameStoreManager.FindAllUEVRGamesAsync(false));
			SortGames();

			// Check if hardware scheduling is enabled and warn the user
			var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);

			if (hklm != null) {
				var keyOpenXRRoot = hklm.OpenSubKey(REGKEY_GRAPHICS, false);
				if (keyOpenXRRoot != null) {
					string hwSchMode = keyOpenXRRoot.GetValue(REGKEY_NAME_SCHEDULER)?.ToString();
					if (hwSchMode == "2") {
						VM.Warning = "Consider disabling 'Hardware Accelerated GPU Scheduling' in your Windows 'Graphics settings', only if you have issues in games";
					}
				}
			}

			hotKeyCheckTimer.Start();
		} catch (Exception ex) {
			await VM.HandleExceptionAsync(this.XamlRoot, ex, "Startup");
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

			InitSort();
			VM.Games.Clear();
			var games = await GameStoreManager.FindAllUEVRGamesAsync(true);
			foreach (var game in games) VM.Games.Add(game);
			SortGames();
		} catch (Exception ex) {
			await VM.HandleExceptionAsync(this.XamlRoot, ex, "Rescan");
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
	async void UpdateUEVR_Click(object sender, RoutedEventArgs e) {
		try {
			VM.IsLoading = true;

			Logger.Log.LogInformation("Starting UEVR Nightly update");
			await Injector.UpdateBackendAsync();
			Logger.Log.LogInformation("Nightly update successful");
		} catch (Exception ex) {
			await VM.HandleExceptionAsync(this.XamlRoot, ex, "Download UEVR Nightly");
		}

		VM.IsLoading = false;
	}
	#endregion
}
