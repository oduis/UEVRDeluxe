#region Usings
using Microsoft.Extensions.Logging;
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
			Logger.Log.LogTrace($"Opening profile page {VM.GameInstallation?.Name}");

			VM.LocalProfile = LocalProfile.FromUnrealVRProfile(VM.GameInstallation.EXEName, true);

			SetRadioButtonValue(spRenderingMethod, VM.LocalProfile.Config.Global["VR_RenderingMethod"]);
			SetRadioButtonValue(spSyncedSequentialMethod, VM.LocalProfile.Config.Global["VR_SyncedSequentialMethod"]);
			cbNativeStereoFix.IsChecked = bool.Parse(VM.LocalProfile.Config.Global["VR_NativeStereoFix"] ?? "false");
			cbNativeStereoFixSamePass.IsChecked = bool.Parse(VM.LocalProfile.Config.Global["VR_NativeStereoFixSamePass"] ?? "false");
			cbGhostingFix.IsChecked = bool.Parse(VM.LocalProfile.Config.Global["VR_GhostingFix"] ?? "false");
			cbAimMPSupport.IsChecked = bool.Parse(VM.LocalProfile.Config.Global["VR_AimMPSupport"] ?? "false");
			cbEnableDepth.IsChecked = bool.Parse(VM.LocalProfile.Config.Global["VR_EnableDepth"] ?? "false");
			cbSnapTurn.IsChecked = bool.Parse(VM.LocalProfile.Config.Global["VR_SnapTurn"] ?? "false");
			slSnapturnTurnAngle.Value = int.Parse(VM.LocalProfile.Config.Global["VR_SnapturnTurnAngle"] ?? "45");
			slResolutionScale.Value = Math.Round(double.Parse(VM.LocalProfile.Config.Global["OpenXR_ResolutionScale"] ?? "1.0", CultureInfo.InvariantCulture) * 100);

			// AntiAlias is special, since a set of values must be used. And it may be configured manually
			string antiAliasingMethod = VM.LocalProfile.CVarsStandard.Global["Renderer_r.AntiAliasingMethod"];
			if (antiAliasingMethod == "4") {
				SetRadioButtonValue(spUpscaler, "1");
				spUpscaler.Visibility = Visibility.Visible;
			} else if (string.IsNullOrEmpty(antiAliasingMethod)) {
				SetRadioButtonValue(spUpscaler, "0");
				spUpscaler.Visibility = Visibility.Visible;
			} else {
				// Upscaling set to a very specific value. Don't touch that.
				spUpscaler.Visibility = Visibility.Collapsed;
			}

			slScreenPercentage.Value = Math.Round(double.Parse(VM.LocalProfile.CVarsStandard.Global["Core_r.ScreenPercentage"] ?? "80.0", CultureInfo.InvariantCulture));

			// Load CVarsData settings
			string oneFrameThreadLag = VM.LocalProfile.CVarsData.Global["Engine_r.OneFrameThreadLag"];
			if (string.IsNullOrEmpty(oneFrameThreadLag)) {
				cbOneFrameThreadLag.IsChecked = null;
			} else {
				cbOneFrameThreadLag.IsChecked = oneFrameThreadLag == "1";
			}
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
		VM.LocalProfile.Config.Global["VR_NativeStereoFix"] = cbNativeStereoFix.IsChecked.ToString().ToLower();
		VM.LocalProfile.Config.Global["VR_NativeStereoFixSamePass"] = cbNativeStereoFixSamePass.IsChecked.ToString().ToLower();
		VM.LocalProfile.Config.Global["VR_GhostingFix"] = cbGhostingFix.IsChecked.ToString().ToLower();
		VM.LocalProfile.Config.Global["VR_AimMPSupport"] = cbAimMPSupport.IsChecked.ToString().ToLower();
		VM.LocalProfile.Config.Global["VR_EnableDepth"] = cbEnableDepth.IsChecked.ToString().ToLower();
		VM.LocalProfile.Config.Global["VR_SnapTurn"] = cbSnapTurn.IsChecked.ToString().ToLower();
		VM.LocalProfile.Config.Global["VR_SnapturnTurnAngle"] = slSnapturnTurnAngle.Value.ToString();
		VM.LocalProfile.Config.Global["OpenXR_ResolutionScale"] = Math.Round(slResolutionScale.Value / 100, 2).ToString(CultureInfo.InvariantCulture);

		if (spUpscaler.Visibility == Visibility.Visible) {
			if (GetRadioButtonValue(spUpscaler) == "1") {
				VM.LocalProfile.CVarsStandard.Global["Renderer_r.AntiAliasingMethod"] = "4"; // TSR/TXAA
				VM.LocalProfile.CVarsStandard.Global["Renderer_r.TemporalAA.Upscaler"] = "1";  // GTemporalUpscaler which may be overridden by a third party plugin
				VM.LocalProfile.CVarsStandard.Global["Renderer_r.TemporalAA.Algorithm"] = "1";  // GTemporalUpscaler which may be overridden by a third party plugin (default)
				VM.LocalProfile.CVarsStandard.Global["Core_r.ScreenPercentage"] = Math.Round(slScreenPercentage.Value).ToString(CultureInfo.InvariantCulture);
				VM.LocalProfile.CVarsStandard.Global["Renderer_r.Upscale.Quality"] = "4";   // 13-tap Lanczos 3 
			} else {
				// Remove the whole set to let the game decide
				VM.LocalProfile.CVarsStandard.Global.RemoveKey("Renderer_r.AntiAliasingMethod");
				VM.LocalProfile.CVarsStandard.Global.RemoveKey("Renderer_r.TemporalAA.Upscaler");
				VM.LocalProfile.CVarsStandard.Global.RemoveKey("Renderer_r.TemporalAA.Algorithm");
				VM.LocalProfile.CVarsStandard.Global.RemoveKey("Core_r.ScreenPercentage");
				VM.LocalProfile.CVarsStandard.Global.RemoveKey("Renderer_r.Upscale.Quality");
			}
		}

		// Save CVarsData settings
		if (cbOneFrameThreadLag.IsChecked.HasValue) {
			VM.LocalProfile.CVarsData.Global["Engine_r.OneFrameThreadLag"] = cbOneFrameThreadLag.IsChecked.Value ? "1" : "0";
		} else {
			VM.LocalProfile.CVarsData.Global.RemoveKey("Engine_r.OneFrameThreadLag");
		}

		if (VM.DescriptionMD != null && VM.DescriptionMD != LocalProfile.DUMMY_DESCRIPTION_MD) {
			VM.LocalProfile.DescriptionMD = VM.DescriptionMD;
		} else {
			VM.LocalProfile.DescriptionMD = null;  // Remove the dummy description
		}

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

			// If the profile meta declares files to be copied to the game folder, try to uninstall them first
			if (VM.LocalProfile?.Meta?.FileCopies?.Any() == true && !string.IsNullOrWhiteSpace(VM.GameInstallation?.EXEPath)) {
				try {
					await CmdManager.UninstallAsync(VM.LocalProfile.FolderPath, Path.GetDirectoryName(VM.GameInstallation.EXEPath));
				} catch (Exception exUninstall) {
					// Ask the user if they want to continue deleting the local profile even if uninstall failed
					var errDialog = new ContentDialog {
						Title = "Uninstall error",
						Content = $"Failed to uninstall profile files from game folder:\n{exUninstall.Message}\n\nContinue to delete the local profile anyway?",
						PrimaryButtonText = "Yes",
						CloseButtonText = "No",
						DefaultButton = ContentDialogButton.Close,
						XamlRoot = this.XamlRoot
					};

					var errResult = await errDialog.ShowAsync();
					if (errResult != ContentDialogResult.Primary) {
						VM.IsLoading = false;
						return; // abort deletion
					}
				}
			}

			VM.LocalProfile.Delete();

			VM.IsLoading = false;

			Frame.GoBack();
		} catch (Exception ex) {
			await VM.HandleExceptionAsync(this.XamlRoot, ex, "Delete error");
		}
	}
	void Back_Click(object sender, RoutedEventArgs e) => Frame.GoBack();
}
