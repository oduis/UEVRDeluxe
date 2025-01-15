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
			VM.IsLoading = true;

			Logger.Log.LogTrace("Opening settings page");

			VM.DelayBeforeInjection = AppUserSettings.DEFAULT_DELAY_BEFORE_INJECTION_SEC;
			string appSetting = AppUserSettings.Read("DelayBeforeInjectionSec");
			if (int.TryParse(appSetting, out int iAppSetting) && iAppSetting > 0) VM.DelayBeforeInjection = iAppSetting;

			VM.OpenXRRuntimes = OpenXRManager.GetAllRuntimes();
			var defaultRuntime = VM.OpenXRRuntimes.FirstOrDefault(r => r.IsDefault);
			if (defaultRuntime != null) VM.SelectedRuntime = defaultRuntime;

			VM.IsLoading = false;
		} catch (Exception ex) {
			VM.IsLoading = false;
			await VM.HandleExceptionAsync(this.XamlRoot, ex, "Load settings error");
		}
	}

	protected override void OnNavigatingFrom(NavigatingCancelEventArgs e) {
		base.OnNavigatingFrom(e);

		AppUserSettings.Write("DelayBeforeInjectionSec", VM.DelayBeforeInjection.ToString());
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

