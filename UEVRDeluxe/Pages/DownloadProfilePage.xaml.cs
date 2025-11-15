#region Usings
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UEVRDeluxe.Code;
using UEVRDeluxe.Common;
using UEVRDeluxe.ViewModels;
#endregion

namespace UEVRDeluxe.Pages;

public sealed partial class DownloadProfilePage : Page {
	public DownloadProfilePageVM VM = new();

	public DownloadProfilePage() { this.InitializeComponent(); }

	protected override void OnNavigatedTo(NavigationEventArgs e) {
		base.OnNavigatedTo(e);

		var para = e.Parameter as DownloadProfilePageArgs;
		VM.GameEXEPath = para.GameEXEPath;
		VM.ProfileMetas = para.ProfileMetas;
		if (VM.ProfileMetas.Count == 1) VM.SelectedProfileMeta = VM.ProfileMetas[0];
	}

	async void Download_Click(object sender, RoutedEventArgs e) {
		try {
			VM.IsLoading = true;

			byte[] profileZip = await AzureManager.DownloadProfileZipAsync(VM.SelectedProfileMeta.EXEName, VM.SelectedProfileMeta.ID);

			// Try to uninstall old profile first, if it had PAKs
			var oldProfile = LocalProfile.FromUnrealVRProfile(VM.OriginalEXEName);
			if (oldProfile?.Meta?.FileCopies?.Any() == true) {
				try {
					await CmdManager.UninstallAsync(oldProfile.FolderPath, Path.GetDirectoryName(VM.GameEXEPath));
				} catch (Exception exUninstall) {
					Logger.Log.LogCritical(exUninstall, "Uninstall failed");
				}
			}

			LocalProfile.ReplaceFromZip(VM.OriginalEXEName, profileZip);

			var newProfile = LocalProfile.FromUnrealVRProfile(VM.OriginalEXEName);
			if (newProfile?.Meta?.FileCopies?.Any() == true) {
				try {
					await CmdManager.InstallAsync(newProfile.FolderPath, Path.GetDirectoryName(VM.GameEXEPath));
				} catch (Exception exInstall) {
					throw new Exception($"Install of profile file copies (PAK) failed: {exInstall.Message}", exInstall);
				}
			}

			VM.IsLoading = false;
			Frame.GoBack();
		} catch (Exception ex) {
			await VM.HandleExceptionAsync(this.XamlRoot, ex, "Download error");
		}
	}

	async void ShowDescription_Click(object sender, RoutedEventArgs e) {
		try {
			VM.IsLoading = true;

			string descriptionMD = await AzureManager.DownloadProfileDescriptionAsync(VM.SelectedProfileMeta.EXEName, VM.SelectedProfileMeta.ID);

			var dialog = new ContentDialog {
				XamlRoot = this.XamlRoot,
				Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
				Title = "Profile Description",
				PrimaryButtonText = "Close", DefaultButton = ContentDialogButton.Primary,
			};

			// No other way to change the size maximum Size of a ContentDialog
			// Use the controls in the page to size it, up to this max size
			dialog.Resources["ContentDialogMaxWidth"] = 1000;
			dialog.Content = new ProfileDescrPage { descriptionMD = descriptionMD };

			var result = await dialog.ShowAsync();

			VM.IsLoading = false;
		} catch (Exception ex) {
			await VM.HandleExceptionAsync(this.XamlRoot, ex, "Download description error");
		}
	}

	void Back_Click(object sender, RoutedEventArgs e) => Frame.GoBack();
}


public class DownloadProfilePageArgs {
	public string GameEXEPath;
	public ObservableCollection<ProfileMeta> ProfileMetas;
}