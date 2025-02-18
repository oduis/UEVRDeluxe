#region Usings
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using UEVRDeluxe.Code;
using UEVRDeluxe.ViewModels;
using Windows.Media.SpeechRecognition;
using Windows.System;
#endregion

namespace UEVRDeluxe.Pages;

public sealed partial class EditVoiceCommandsPage : Page {
	readonly EditVoiceCommandsPageVM VM = new();

	public EditVoiceCommandsPage() {
		this.InitializeComponent();
		this.Loaded += Page_Loaded;
	}

	protected override void OnNavigatedTo(NavigationEventArgs e) {
		base.OnNavigatedTo(e);

		VM.EXEName = e.Parameter as string;
	}

	async void Page_Loaded(object sender, RoutedEventArgs e) {
		try {
			Logger.Log.LogTrace($"Opening voice command page {VM.EXEName}");

			var recognizer = new SpeechRecognizer();
			var languages = SpeechRecognizer.SupportedTopicLanguages;
			if (languages.Count == 0) {
				OpenWinLanguages_Click(null, null);
				throw new Exception("Please install Speech Recognition within the language in Windows settings first");
			}

			VM.Languages = [.. languages];
			VM.SelectedLanguage = VM.Languages[0];
		} catch (Exception ex) {
			await VM.HandleExceptionAsync(this.XamlRoot, ex, "Edit Voice Commands Error");
		}
	}

	/// <summary>Open the settings page to install a Speech Recognition language
	async void OpenWinLanguages_Click(object sender, RoutedEventArgs e) {
		await Launcher.LaunchUriAsync(new Uri("ms-settings:regionlanguage"));
	}

	async void Save_Click(object sender, RoutedEventArgs e) {
		try {
			VM.IsLoading = true;

			// TODO
			VM.IsLoading = false;

			Frame.GoBack();
		} catch (Exception ex) {
			await VM.HandleExceptionAsync(this.XamlRoot, ex, "Save error");
		}
	}

	void Back_Click(object sender, RoutedEventArgs e) { Frame.GoBack(); }
}
