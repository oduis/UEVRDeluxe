#region Usings
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.ObjectModel;
using System.IO;
using UEVRDeluxe.Code;
using UEVRDeluxe.Common;
using UEVRDeluxe.ViewModels;
using Windows.Storage;
using Windows.Storage.Pickers;
#endregion

namespace UEVRDeluxe.Pages;

public sealed partial class AdminPage : Page {
	public AdminPageVM VM = new();

	public AdminPage() { this.InitializeComponent(); }

	async void Upload_Click(object sender, RoutedEventArgs e) {
		var picker = new FolderPicker();
		picker.SuggestedStartLocation = PickerLocationId.Desktop;
		picker.FileTypeFilter.Add("*");

		//var hWnd = XamlRoot.ContentIslandEnvironment.AppWindowId;
		WinRT.Interop.InitializeWithWindow.Initialize(picker, MainWindow.hWnd);

		// If it crashes here while debugging, run VS as normal user, NOT admin
		var folder = await picker.PickSingleFolderAsync();
		if (folder == null) return;

		try {
			VM.IsLoading = true;

			var localProfile = new LocalProfile(folder.Path);

			byte[] zippedProfile = await localProfile.PrepareForSubmitAsync();
			VM.ProfileMetas = new ObservableCollection<ProfileMeta>(await AzureManager.UploadProfileAsync(zippedProfile));

			VM.SearchEXEName = localProfile.Meta.EXEName;

			VM.IsLoading = false;

			await new ContentDialog {
				Title = "Upload", Content = "Profile uploaded successfully", CloseButtonText = "OK", XamlRoot = this.XamlRoot
			}.ShowAsync();
		} catch (Exception ex) {
			await VM.HandleExceptionAsync(this.XamlRoot, ex, "Upload error");
		}
	}

	async void Search_Click(object sender, RoutedEventArgs e) {
		try {
			if (string.IsNullOrEmpty(VM.SearchEXEName)) throw new Exception("Give the full EXE-Name (without extensions)");
			VM.SearchEXEName = VM.SearchEXEName.Trim();

			VM.IsLoading = true;

			VM.ProfileMetas = new ObservableCollection<ProfileMeta>(await AzureManager.SearchProfilesAsync(VM.SearchEXEName, false, true));

			VM.IsLoading = false;
		} catch (Exception ex) {
			await VM.HandleExceptionAsync(this.XamlRoot, ex, "Search error");
		}
	}

	async void Download_Click(object sender, RoutedEventArgs e) {
		try {
			VM.IsLoading = true;

			byte[] profileZip = await AzureManager.DownloadProfileZipAsync(VM.SelectedProfileMeta.EXEName, VM.SelectedProfileMeta.ID);

			VM.IsLoading = false;

			var picker = new FileSavePicker();
			picker.FileTypeChoices.Add("ZIP File", [".zip"]);
			picker.DefaultFileExtension = ".zip";
			picker.SuggestedFileName = AzConstants.GetProfileFileName(VM.SelectedProfileMeta.ID, VM.SelectedProfileMeta.EXEName);

			WinRT.Interop.InitializeWithWindow.Initialize(picker, MainWindow.hWnd);

			var saveFile = await picker.PickSaveFileAsync();
			if (saveFile != null) {
				using (var stream = await saveFile.OpenStreamForWriteAsync()) {
					await stream.WriteAsync(profileZip, 0, profileZip.Length);
				}
			}

		} catch (Exception ex) {
			await VM.HandleExceptionAsync(this.XamlRoot, ex, "Download error");
		}
	}

	async void Delete_Click(object sender, RoutedEventArgs e) {
		var confirmDialog = new ContentDialog {
			Title = "Delete profile",
			Content = "Are you sure you want to delete this profile from the cloud database?",
			PrimaryButtonText = "Yes", CloseButtonText = "No", DefaultButton = ContentDialogButton.Close,
			XamlRoot = this.XamlRoot
		};

		var result = await confirmDialog.ShowAsync();
		if (result != ContentDialogResult.Primary) return;
		try {
			VM.IsLoading = true;

			await AzureManager.DeleteProfileAsync(VM.SelectedProfileMeta.EXEName, VM.SelectedProfileMeta.ID);

			VM.ProfileMetas.Remove(VM.SelectedProfileMeta);
			VM.SelectedProfileMeta = null;

			VM.IsLoading = false;

			await new ContentDialog {
				Title = "Delete", Content = "Profile deleted successfully", CloseButtonText = "OK", XamlRoot = this.XamlRoot
			}.ShowAsync();
		} catch (Exception ex) {
			await VM.HandleExceptionAsync(this.XamlRoot, ex, "Delete error");
		}
	}

	void Back_Click(object sender, RoutedEventArgs e) => Frame.GoBack();
}
