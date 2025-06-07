#region Usings
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.ObjectModel;
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

		var para= e.Parameter as DownloadProfilePageArgs;
		VM.OriginalEXEName = para.OriginalEXEName;
		VM.ProfileMetas = para.ProfileMetas;
		if (VM.ProfileMetas.Count == 1) VM.SelectedProfileMeta = VM.ProfileMetas[0];
	}

	async void Download_Click(object sender, RoutedEventArgs e) {
		try {
			VM.IsLoading = true;

			byte[] profileZip = await AzureManager.DownloadProfileZipAsync(VM.SelectedProfileMeta.EXEName, VM.SelectedProfileMeta.ID);

			LocalProfile.ReplaceFromZip(VM.OriginalEXEName, profileZip);

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
	public string OriginalEXEName;
	public ObservableCollection<ProfileMeta> ProfileMetas;
}