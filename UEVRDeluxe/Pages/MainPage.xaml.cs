#region Usings
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Linq;
using System.Security;
using System.Threading.Tasks;
using UEVRDeluxe.Code;
using UEVRDeluxe.ViewModels;
#endregion

namespace UEVRDeluxe.Pages;

public sealed partial class MainPage : Page {
	const string REGKEY_GRAPHICS = @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers";
	const string REGKEY_NAME_SCHEDULER = "HwSchMode";

	MainPageVM VM = new();

	public MainPage() { this.InitializeComponent(); }

	async void Page_Loaded(object sender, RoutedEventArgs e) {
		try {
			VM.IsLoading = true;
			VM.Games = GameStoreManager.FindAllUEVRGames();

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
		} catch (Exception ex) {
			await HandleExceptionAsync(ex, "Steam load");
		}

		VM.IsLoading = false;
	}

	async void OpenXRRuntimes_SelectionChanged(object sender, SelectionChangedEventArgs e) {
		if (VM.IsLoading || e.AddedItems.Count != 1) return;  // Still in initialisation

		try {
			OpenXRManager.SetActiveRuntime((e.AddedItems.First() as OpenXRRuntime).Path);
		} catch (Exception ex) {
			await HandleExceptionAsync(ex, "Runtime switcher");
		}
	}

	void NavigateAdminPage(object sender, RoutedEventArgs e) =>
		Frame.Navigate(typeof(AdminPage), null, new DrillInNavigationTransitionInfo());

	void GamesView_ItemClick(object sender, ItemClickEventArgs e)
		=> Frame.Navigate(typeof(GamePage), e.ClickedItem, new SlideNavigationTransitionInfo { Effect = SlideNavigationTransitionEffect.FromRight });

	async Task HandleExceptionAsync(Exception ex, string title) {
		VM.IsLoading = false;

		string message = ex.Message;
		if (ex is SecurityException) message = $"Security error: {message}\r\nYou might want to start UEVR Deluxe as administrator";

		await new ContentDialog {
			Title = title, CloseButtonText = "OK", XamlRoot = this.XamlRoot,
			Content = string.IsNullOrEmpty(ex.Message) ? ex.ToString() : ex.Message
		}.ShowAsync();
	}
}
