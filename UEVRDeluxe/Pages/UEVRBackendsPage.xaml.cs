#region Usings
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;
using UEVRDeluxe.Code;
using UEVRDeluxe.ViewModels;
#endregion

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace UEVRDeluxe.Pages;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class UEVRBackendsPage : Page {
	readonly UEVRBackendsPageVM VM = new();

	#region * Init
	public UEVRBackendsPage() { InitializeComponent(); }

	async void Page_Loaded(object sender, RoutedEventArgs e) {
		try {
			VM.IsLoading = true;

			await RefreshVersionLabelsAsync();
		} catch (Exception ex) {
			await VM.HandleExceptionAsync(this.XamlRoot, ex, "Startup");
		}

		VM.IsLoading = false;
	}
	#endregion

	#region UpdateUEVR
	async Task RefreshVersionLabelsAsync() {
		VM.PraydogInstalled = Injector.GetInstalledUEVRNightlyNumber()?.ToString() ?? "<unknown>";

		try {
			VM.PraydogLatest = (await Injector.ReadLatestUEVRNightlyNumberAsync()).ToString();
		} catch (Exception ex) {
			Logger.Log.LogError(ex, "Failed to read latest UEVR nightly number");
			VM.PraydogLatest = "<unknown>";
		}


		VM.JoeyHodgeInstalled = Injector.GetInstalledUEVRJoeyHodgeName() ?? "<unknown>";
		try {
			VM.JoeyHodgeLatest = await Injector.ReadLatestUEVRJoeyHodgeVersionAsync();
		} catch (Exception ex) {
			Logger.Log.LogError(ex, "Failed to read latest UEVR JoeyHodge version");
			VM.JoeyHodgeLatest = "<unknown>";
		}


		VM.PureDarkInstalled = Injector.GetInstalledUEVRPureDarkName() ?? "<unknown>";
		try {
			VM.PureDarkLatest = await Injector.ReadLatestUEVRPureDarkVersionAsync();
		} catch (Exception ex) {
			Logger.Log.LogError(ex, "Failed to read latest UEVR PureDark version");
			VM.PureDarkLatest = "<unknown>";
		}
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

	async void UpdateUEVRPraydog_Click(object sender, RoutedEventArgs e) {
		try {
			VM.IsLoading = true;

			int latestNightlyNumber = await Injector.ReadLatestUEVRNightlyNumberAsync();
			int? installedNightlyNumber = Injector.GetInstalledUEVRNightlyNumber();

			int? nightlyNumber = await ShowUpdateNightlyDialogAsync(installedNightlyNumber, latestNightlyNumber);
			if (nightlyNumber == -1) { VM.IsLoading = false; return; }

			Logger.Log.LogInformation($"Starting UEVR Nightly update (nightly: {nightlyNumber?.ToString() ?? "latest"})");

			await CmdManager.UpdateBackendAsync(nightlyNumber ?? latestNightlyNumber);

			await RefreshVersionLabelsAsync();

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

	async Task<string> ShowUpdateNonPrayDogDialogAsync(string installedTagName, string latestTagName, string placeholderText) {
		bool latestInstalled = string.Equals(installedTagName, latestTagName, StringComparison.OrdinalIgnoreCase);
		var radioLatest = new RadioButton {
			Content = $"Latest version ({latestTagName}{(latestInstalled ? ", already installed" : "")})", IsChecked = !latestInstalled
		};
		var radioSpecific = new RadioButton {
			Content = "Specific tag:", IsChecked = latestInstalled
		};

		// not in the same parent, so do it manually
		radioLatest.Checked += (object s, RoutedEventArgs e) => radioSpecific.IsChecked = false;
		radioSpecific.Checked += (s, e) => radioLatest.IsChecked = false;

		var tagNameBox = new TextBox { PlaceholderText = placeholderText, Width = 220 };
		tagNameBox.TextChanged += (s, e) => { if (!string.IsNullOrEmpty(tagNameBox.Text)) radioSpecific.IsChecked = true; };

		var spSpecific = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
		spSpecific.Children.Add(radioSpecific);
		spSpecific.Children.Add(tagNameBox);

		var errorText = new TextBlock {
			Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red), Visibility = Visibility.Collapsed
		};

		var spMain = new StackPanel { Spacing = 8 };
		spMain.Children.Add(radioLatest);
		spMain.Children.Add(spSpecific);
		spMain.Children.Add(errorText);

		var dialog = new ContentDialog {
			Title = "Update JoeHodge Backend to", XamlRoot = this.XamlRoot,
			PrimaryButtonText = "Update", CloseButtonText = "Cancel",
			Content = spMain
		};

		string resultTag = null;
		dialog.PrimaryButtonClick += (s, e) => {
			if (radioLatest.IsChecked == true) {
				resultTag = latestTagName;
			} else if (radioSpecific.IsChecked == true && !string.IsNullOrEmpty(tagNameBox.Text)) {
				resultTag = tagNameBox.Text;
			} else {
				errorText.Text = "Please enter a valid tag";
				errorText.Visibility = Visibility.Visible;
				e.Cancel = true;
			}
		};

		var result = await dialog.ShowAsync();
		if (result != ContentDialogResult.Primary) return null; // Cancelled
		return resultTag;
	}

	async void UpdateUEVRJoeyHodge_Click(object sender, RoutedEventArgs e) {
		try {
			VM.IsLoading = true;

			string latestJoeyHodgeName = await Injector.ReadLatestUEVRJoeyHodgeVersionAsync();
			string installedJoeyHodgeName = Injector.GetInstalledUEVRJoeyHodgeName();

			string tagName = await ShowUpdateNonPrayDogDialogAsync(installedJoeyHodgeName, latestJoeyHodgeName, "e.g. subnauticaharden");
			if (tagName == null) { VM.IsLoading = false; return; }

			Logger.Log.LogInformation($"Starting UEVR JoeyHodge update (version: {tagName})");

			await CmdManager.UpdateJoeyHodgeBackendAsync(tagName);

			await RefreshVersionLabelsAsync();

			VM.IsLoading = false;

			await new ContentDialog {
				Title = "UEVR JoeyHodge", CloseButtonText = "OK", XamlRoot = this.XamlRoot,
				Content = "Updated successfully"
			}.ShowAsync();
		} catch (Exception ex) {
			VM.IsLoading = false;
			await VM.HandleExceptionAsync(this.XamlRoot, ex, "Download UEVR JoeyHodge");
		}
	}

	async void UpdateUEVRPureDark_Click(object sender, RoutedEventArgs e) {
		try {
			VM.IsLoading = true;

			string latestPureDarkName = await Injector.ReadLatestUEVRPureDarkVersionAsync();
			string installedPureDarkName = Injector.GetInstalledUEVRPureDarkName();

			string tagName = await ShowUpdateNonPrayDogDialogAsync(installedPureDarkName, latestPureDarkName, "UEVR_AFW_v1.0-beta.3");
			if (tagName == null) { VM.IsLoading = false; return; }

			Logger.Log.LogInformation($"Starting UEVR PureDark update (version: {tagName})");

			await CmdManager.UpdatePureDarkBackendAsync(tagName);

			await RefreshVersionLabelsAsync();

			VM.IsLoading = false;

			await new ContentDialog {
				Title = "UEVR PureDark", CloseButtonText = "OK", XamlRoot = this.XamlRoot,
				Content = "Updated successfully"
			}.ShowAsync();
		} catch (Exception ex) {
			VM.IsLoading = false;
			await VM.HandleExceptionAsync(this.XamlRoot, ex, "Download UEVR PureDark");
		}
	}
	#endregion

	void Back_Click(object sender, RoutedEventArgs e) => Frame.GoBack();
}
