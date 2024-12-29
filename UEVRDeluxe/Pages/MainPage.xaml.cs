#region Usings
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using System.Linq;
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

		VM.OpenXRRuntimes = OpenXRManager.GetAllRuntimes();
		var defaultRuntime = VM.OpenXRRuntimes.FirstOrDefault(r => r.IsDefault);
		if (defaultRuntime != null) VM.SelectedRuntime = defaultRuntime;

		VM.IsLoading = false;
	}

	void OpenXRRuntimes_SelectionChanged(object sender, SelectionChangedEventArgs e) {
		if (VM.IsLoading || e.AddedItems.Count != 1) return;  // Still in initialisation
		OpenXRManager.SetActiveRuntime((e.AddedItems.First() as OpenXRRuntime).Path);
	}

	void NavigateAdminPage(object sender, RoutedEventArgs e) =>
		Frame.Navigate(typeof(AdminPage), null, new DrillInNavigationTransitionInfo());

	void GamesView_ItemClick(object sender, ItemClickEventArgs e)
		=> Frame.Navigate(typeof(GamePage), e.ClickedItem, new SlideNavigationTransitionInfo { Effect = SlideNavigationTransitionEffect.FromRight });
}
