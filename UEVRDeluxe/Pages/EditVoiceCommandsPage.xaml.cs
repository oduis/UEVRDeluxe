#region Usings
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using Windows.Media.SpeechRecognition;
using UEVRDeluxe.Code;
using UEVRDeluxe.ViewModels;
using System.Collections.Generic;
using Windows.Globalization;
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
			var recognizer = new SpeechRecognizer();
			var languages = SpeechRecognizer.SupportedTopicLanguages;
			if (languages.Count == 0) throw new Exception("Please install a Speech Recognition language in Windows settings first");
			VM.Languages = new List<Language>(languages);


		} catch (Exception ex) {
			await VM.HandleExceptionAsync(this.XamlRoot, ex, "Load profile error");
		}
	}
}
