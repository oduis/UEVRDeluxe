#region Usings
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.Win32;
using System;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Security;
using System.Threading.Tasks;
using UEVRDeluxe.Code;
using UEVRDeluxe.Common;
using UEVRDeluxe.ViewModels;
#endregion

namespace UEVRDeluxe.Pages;

public sealed partial class MainPage : Page {
	const string REGKEY_GRAPHICS = @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers";
	const string REGKEY_NAME_SCHEDULER = "HwSchMode";

	MainPageVM VM = new();

	#region * Init
	public MainPage() { this.InitializeComponent(); }

	async void Page_Loaded(object sender, RoutedEventArgs e) {
		try {
			VM.IsLoading = true;

			await CheckVersionAsync();

			VM.Games = await GameStoreManager.FindAllUEVRGamesAsync();

			VM.OpenXRRuntimes = OpenXRManager.GetAllRuntimes();
			var defaultRuntime = VM.OpenXRRuntimes.FirstOrDefault(r => r.IsDefault);
			if (defaultRuntime != null) VM.SelectedRuntime = defaultRuntime;

			// Check if hardware scheduling is enabled and warn the user
			var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);

			if (hklm != null) {
				var keyOpenXRRoot = hklm.OpenSubKey(REGKEY_GRAPHICS, false);
				if (keyOpenXRRoot != null) {
					string hwSchMode = keyOpenXRRoot.GetValue(REGKEY_NAME_SCHEDULER)?.ToString();
					if (hwSchMode == "2") {
						VM.Warning = "Consider disabling 'Hardware Accelerated GPU Scheduling' in your Windows 'Graphics settings' if you have issues in games";
					}
				}
			}

			gvGames.Focus(FocusState.Programmatic);  // WInUI selects links otherwise
		} catch (Exception ex) {
			await VM.HandleExceptionAsync(this.XamlRoot, ex, "Steam load");
		}

		VM.IsLoading = false;
	}
	#endregion

	#region OpenXR
	async void OpenXRRuntimes_SelectionChanged(object sender, SelectionChangedEventArgs e) {
		if (VM.IsLoading || e.AddedItems.Count != 1) return;  // Still in initialisation

		try {
			OpenXRManager.SetActiveRuntime((e.AddedItems.First() as OpenXRRuntime).Path);
		} catch (Exception ex) {
			await VM.HandleExceptionAsync(this.XamlRoot, ex, "Runtime switcher");
		}
	}
	#endregion

	#region * Link Handler
	void NavigateAdminPage(object sender, RoutedEventArgs e)
		=> Frame.Navigate(typeof(AdminPage), null, new DrillInNavigationTransitionInfo());

	void NavigateAllProfilesPage(object sender, RoutedEventArgs e)
	=> Frame.Navigate(typeof(AllProfilesPage), null, new DrillInNavigationTransitionInfo());

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
				System.Diagnostics.Debug.WriteLine("Starting version check");

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
								Content = ($"An update ot UEVR Deluxe is available. Would you like to download the update now?\n{autoupdate.ReleaseNotes}").Trim()
							}.ShowAsync();

							if (dialogResult == ContentDialogResult.Primary)
								await Windows.System.Launcher.LaunchUriAsync(new Uri(autoupdate.WebURL));
						}
					}
				}
			}
		} catch (Exception ex) {
			// Gracefully ignore
			System.Diagnostics.Debug.WriteLine($"Cannot check version: {ex.Message}");
		}
	}
	#endregion
}
