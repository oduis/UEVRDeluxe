#region Usings
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using UEVRDeluxe.Code;
using UEVRDeluxe.Common;
using UEVRDeluxe.ViewModels;
#endregion

namespace UEVRDeluxe.Pages;

public sealed partial class SettingsPage : Page {
	SettingsPageVM VM = new();

	#region * Init
	public SettingsPage() {
		this.InitializeComponent();
		this.Loaded += Page_Loaded;
	}

	async void Page_Loaded(object sender, RoutedEventArgs e) {
		try {
			Logger.Log.LogTrace("Opening settings page");

			VM.OpenXRRuntimes = OpenXRManager.GetAllRuntimes();
			var defaultRuntime = VM.OpenXRRuntimes.FirstOrDefault(r => r.IsDefault);
			if (defaultRuntime != null) VM.SelectedRuntime = defaultRuntime;
		} catch (Exception ex) {
			await VM.HandleExceptionAsync(this.XamlRoot, ex, "Load settings error");
		}
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

	void Back_Click(object sender, RoutedEventArgs e) => Frame.GoBack();
}

