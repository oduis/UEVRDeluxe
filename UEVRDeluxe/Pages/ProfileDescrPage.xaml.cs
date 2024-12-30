using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace UEVRDeluxe.Pages;

public sealed partial class ProfileDescrPage : Page {
	public string descriptionMD;

	public ProfileDescrPage() {
		this.InitializeComponent();
		this.Loaded += ProfileDescrPage_Loaded;
	}
	/*
	protected override void OnNavigatedTo(NavigationEventArgs e) {
		base.OnNavigatedTo(e);

		this.descriptionMD = e.Parameter as string;
	}
	*/
	async void ProfileDescrPage_Loaded(object sender, RoutedEventArgs e) {
		await PageHelpers.RefreshDescriptionAsync(webViewDescription, descriptionMD);
	}
}