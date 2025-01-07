#region Usings
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UEVRDeluxe.Code;
using UEVRDeluxe.ViewModels;
using Windows.Storage.Pickers;
#endregion

namespace UEVRDeluxe.Pages;

public sealed partial class EditProfilePage : Page {
	EditProfilePageVM VM = new();

	public EditProfilePage() {
		this.InitializeComponent();
		this.Loaded += EditProfilePage_Loaded;
	}

	protected override void OnNavigatedTo(NavigationEventArgs e) {
		base.OnNavigatedTo(e);

		VM.GameInstallation = e.Parameter as GameInstallation;
	}

	async void EditProfilePage_Loaded(object sender, RoutedEventArgs e) {
		try {
			VM.LocalProfile = LocalProfile.FromUnrealVRProfile(VM.GameInstallation.EXEName, true);

			SetRadioButtonValue(spRenderingMethod, VM.LocalProfile.Config.Global["VR_RenderingMethod"]);
			SetRadioButtonValue(spSyncedSequentialMethod, VM.LocalProfile.Config.Global["VR_SyncedSequentialMethod"]);
			cbGhostingFix.IsChecked = bool.Parse(VM.LocalProfile.Config.Global["VR_GhostingFix"] ?? "false");
			cbEnableDepth.IsChecked = bool.Parse(VM.LocalProfile.Config.Global["VR_EnableDepth"] ?? "false");

			slResolutionScale.Value = Math.Round(double.Parse(VM.LocalProfile.Config.Global["OpenXR_ResolutionScale"] ?? "1.0", CultureInfo.InvariantCulture) * 100);
		} catch (Exception ex) {
			await VM.HandleExceptionAsync(this.XamlRoot, ex, "Load profile error");
		}
	}

	async void Save_Click(object sender, RoutedEventArgs e) {
		try {
			VM.IsLoading = true;

			await SaveAsync();
			VM.IsLoading = false;

			Frame.GoBack();
		} catch (Exception ex) {
			await VM.HandleExceptionAsync(this.XamlRoot, ex, "Save error");
		}
	}

	async Task SaveAsync() {
		VM.LocalProfile.Config.Global["VR_RenderingMethod"] = GetRadioButtonValue(spRenderingMethod);
		VM.LocalProfile.Config.Global["VR_SyncedSequentialMethod"] = GetRadioButtonValue(spSyncedSequentialMethod);
		VM.LocalProfile.Config.Global["VR_GhostingFix"] = cbGhostingFix.IsChecked.ToString();
		VM.LocalProfile.Config.Global["VR_EnableDepth"] = cbEnableDepth.IsChecked.ToString();
		VM.LocalProfile.Config.Global["OpenXR_ResolutionScale"] = Math.Round(slResolutionScale.Value / 100, 2).ToString(CultureInfo.InvariantCulture);

		await VM.LocalProfile.SaveAsync();
	}

	void SetRadioButtonValue(StackPanel parent, string tag) =>
		((RadioButton)parent.Children.First(c => (c as RadioButton)?.Tag?.ToString() == tag)).IsChecked = true;

	string GetRadioButtonValue(StackPanel parent) =>
		((RadioButton)parent.Children.First(c => (c as RadioButton)?.IsChecked ?? false)).Tag as string;

	async void Publish_Click(object sender, RoutedEventArgs e) {
		try {
			VM.IsLoading = true;

			byte[] profileZip = await VM.LocalProfile.PrepareForSubmitAsync(VM.GameInstallation);

			VM.IsLoading = false;

			var picker = new FileSavePicker();
			picker.FileTypeChoices.Add("ZIP File", [".zip"]);
			picker.DefaultFileExtension = ".zip";
			picker.SuggestedFileName = VM.GameInstallation.EXEName + ".zip";

			var hWnd = XamlRoot.ContentIslandEnvironment.AppWindowId;
			WinRT.Interop.InitializeWithWindow.Initialize(picker, (int)hWnd.Value);

			var saveFile = await picker.PickSaveFileAsync();
			if (saveFile != null) {
				using (var stream = await saveFile.OpenStreamForWriteAsync()) {
					await stream.WriteAsync(profileZip, 0, profileZip.Length);
				}

				await new ContentDialog {
					Title = "Publish success", Content = "Profile is now packed and ready to submit on Discord #ue-general, if it was tested by some users", CloseButtonText = "OK", XamlRoot = this.XamlRoot
				}.ShowAsync();
			}
		} catch (Exception ex) {
			await VM.HandleExceptionAsync(this.XamlRoot, ex, "Publish error");
		}
	}

	async void OpenFolder_Click(object sender, RoutedEventArgs e) {
		try {
			if (!Directory.Exists(VM.LocalProfile.FolderPath)) return;

			var folderUri = new Uri(VM.LocalProfile.FolderPath);

			var startInfo = new ProcessStartInfo {
				FileName = folderUri.AbsoluteUri,
				UseShellExecute = true,
				Verb = "open"
			};
			Process.Start(startInfo);
		} catch (Exception ex) {
			await VM.HandleExceptionAsync(this.XamlRoot, ex, "Open folder error");
		}
	}

	async void Delete_Click(object sender, RoutedEventArgs e) {
		var confirmDialog = new ContentDialog {
			Title = "Delete profile",
			Content = "Are you sure you want to delete your local profile?\nCan be helpful if UEVR or the game was massively updated since it was built.",
			PrimaryButtonText = "Yes", CloseButtonText = "No", DefaultButton = ContentDialogButton.Close,
			XamlRoot = this.XamlRoot
		};

		var result = await confirmDialog.ShowAsync();
		if (result != ContentDialogResult.Primary) return;
		try {
			VM.IsLoading = true;

			VM.LocalProfile.Delete();

			VM.IsLoading = false;

			Frame.GoBack();
		} catch (Exception ex) {
			await VM.HandleExceptionAsync(this.XamlRoot, ex, "Delete error");
		}
	}
	void Back_Click(object sender, RoutedEventArgs e) => Frame.GoBack();
}
