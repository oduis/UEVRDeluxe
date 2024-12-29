#region Usings
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Threading.Tasks;
using UEVRDeluxe.Pages;
using UEVRDeluxe.Code;
using UEVRDeluxe.ViewModels;
#endregion

namespace UEVRDeluxe.Pages;

public sealed partial class MainPage : Page {
	MainPageVM VM = new();

	public MainPage() { this.InitializeComponent(); }

	void Page_Loaded(object sender, RoutedEventArgs e) {
		VM.IsLoading = true;
		VM.Games = GameStoreManager.FindAllUEVRGames();
		VM.IsLoading = false;
	}

	void NavigateAdminPage(object sender, RoutedEventArgs e) =>
		Frame.Navigate(typeof(AdminPage), null, new DrillInNavigationTransitionInfo());

	void ListView_ItemClick(object sender, ItemClickEventArgs e)
		=> Frame.Navigate(typeof(GamePage), e.ClickedItem, new SlideNavigationTransitionInfo { Effect=SlideNavigationTransitionEffect.FromRight });
}
