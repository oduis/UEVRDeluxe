#region Usings
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using UEVRDeluxe.Code;
using UEVRDeluxe.ViewModels;
#endregion

namespace UEVRDeluxe.Pages;

public sealed partial class AllProfilesPage : Page {
	AllProfilesPageVM VM = new();

	public AllProfilesPage() {
		this.InitializeComponent();
		this.Loaded += AllProfilesPage_Loaded;
	}

	async void AllProfilesPage_Loaded(object sender, RoutedEventArgs e) {
		try {
			VM.IsLoading = true;

			VM.AllProfileNames = await AzureManager.GetAllProfileNamesAsync();

			VM.IsLoading = false;
		} catch (Exception ex) {
			await VM.HandleExceptionAsync(this.XamlRoot, ex, "Load profile error");
		}
	}

	void Back_Click(object sender, RoutedEventArgs e) => Frame.GoBack();
}

