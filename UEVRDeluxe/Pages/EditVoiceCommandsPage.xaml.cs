#region Usings
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using UEVRDeluxe.Code;
using UEVRDeluxe.ViewModels;
using Windows.Media.SpeechRecognition;
using Windows.System;
#endregion

namespace UEVRDeluxe.Pages;

public sealed partial class EditVoiceCommandsPage : Page {
	readonly EditVoiceCommandsPageVM VM = new();

	/// <summary>Anything above on char cannot be mapped using Win32</summary>
	Dictionary<string, int> MAP_KEYNAME_VKKEYCODE = new(StringComparer.OrdinalIgnoreCase) {
		{ "F1", 0x70 }, { "F2", 0x71 }, { "F3", 0x72 }, { "F4", 0x73 },
		{ "F5", 0x74 }, { "F6", 0x75 }, { "F7", 0x76 }, { "F8", 0x77 },
		{ "F9", 0x78 }, { "F10", 0x79 }, { "F11", 0x7A }, { "F12", 0x7B },
		{ "Enter", 0x0D }, { "Escape", 0x1B }, { "Esc", 0x1B }, { "Space", 0x20 }, { "Tab", 0x09 },
		{ "Left", 0x25 }, { "Up", 0x26 }, { "Right", 0x27 }, { "Down", 0x28 },
		{ "PgUp", 0x21 }, { "PgDown", 0x22 }, { "Ins", 0x2D }, { "Del", 0x2E },
		{ "Home", 0x24 }, { "End", 0x23 }, { "Ctrl", 0x11 }, { "Shift", 0x10 }
	};

	IntPtr keyboardLayout;

	public EditVoiceCommandsPage() {
		this.InitializeComponent();
		this.Loaded += Page_Loaded;
	}

	protected override void OnNavigatedTo(NavigationEventArgs e) {
		base.OnNavigatedTo(e);

		VM.EXEName = e.Parameter as string;
	}

	protected override void OnNavigatedFrom(NavigationEventArgs e) {
		base.OnNavigatedFrom(e);
		StopTestRecognizer();
	}

	async void Page_Loaded(object sender, RoutedEventArgs e) {
		try {
			Logger.Log.LogTrace($"Opening voice command page {VM.EXEName}");

			keyboardLayout = Win32.GetKeyboardLayout(0);

			var recognizer = new SpeechRecognizer();
			var languages = SpeechRecognizer.SupportedTopicLanguages;
			if (languages.Count == 0) {
				OpenWinLanguages_Click(null, null);
				throw new Exception("Please install Speech Recognition within the language in Windows settings first");
			}

			VM.Languages = [.. languages];

			string profilePath = VoiceCommandProfile.GetFilePath(VM.EXEName);
			if (File.Exists(profilePath)) {
				var profile = JsonSerializer.Deserialize<VoiceCommandProfile>(File.ReadAllText(profilePath),
					new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

				profile.Migrate();

				slMinConfidence.Value = (int)(profile.MinConfidence * 100);
				VM.KeyPressDelayMS = profile.KeyPressDelayMS > 0 ? profile.KeyPressDelayMS : VoiceCommandProfile.DEFAULT_KEYPRESS_DELAY_MS;

				VM.SelectedLanguage = VM.Languages.FirstOrDefault(l => l.LanguageTag == profile.LanguageTag) ?? VM.Languages[0];

				VM.InjectText = profile.InjectText;
				VM.VoiceCommands = [.. profile.Commands.Select(c => new VoiceCommandEx(c))];
			} else {
				slMinConfidence.Value = 75;
				VM.KeyPressDelayMS = VoiceCommandProfile.DEFAULT_KEYPRESS_DELAY_MS;
				VM.SelectedLanguage = VM.Languages[0];
				VM.VoiceCommands = [];
			}
		} catch (Exception ex) {
			await VM.HandleExceptionAsync(this.XamlRoot, ex, "Edit Voice Commands Error");
		}
	}

	/// <summary>Open the settings page to install a Speech Recognition language
	async void OpenWinLanguages_Click(object sender, RoutedEventArgs e)
		=> await Launcher.LaunchUriAsync(new Uri("ms-settings:regionlanguage"));

	async void AddCommand_Click(object sender, RoutedEventArgs e) {
		try {
			if (string.IsNullOrWhiteSpace(VM.Text))
				throw new Exception("Please enter a text to say in the language selected above, e.g. 'open map'");

			if (string.IsNullOrWhiteSpace(VM.TextKeyCode))
				throw new Exception("Please enter a character or function key to invoke, e.g. 'A' or 'F7' or 'Tab'");

			var newCommand = new VoiceCommandEx { Text = VM.Text.Trim() };

			// Clean up multiple spaces and split by space to get individual keys
			var keyTexts = VM.TextKeyCode.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
			var keyCodes = new List<int>();
			var displayTexts = new List<string>();

			foreach (var keyText in keyTexts) {
				int vkCode;
				string displayText;

				if (keyText.Length == 1) {
					vkCode = Win32.VkKeyScanExW(keyText[0], keyboardLayout);
					displayText = keyText.ToUpper();
				} else {
					if (!MAP_KEYNAME_VKKEYCODE.TryGetValue(keyText, out vkCode))
						throw new Exception($"Unknown key code: {keyText}");
					displayText = char.ToUpper(keyText[0]) + keyText.Substring(1).ToLower();
				}

				keyCodes.Add(vkCode);
				displayTexts.Add(displayText);
			}

			newCommand.VKKeyCodes = [.. keyCodes];
			newCommand.TextKeyCode = string.Join(" ", displayTexts);

			RemoveCommand(newCommand.Text);
			VM.VoiceCommands.Add(newCommand);

			VM.Text = VM.TextKeyCode = string.Empty;
		} catch (Exception ex) {
			await VM.HandleExceptionAsync(this.XamlRoot, ex, "Add voice command error");
		}
	}

	void RemoveCommand_Click(object sender, RoutedEventArgs e)
		=> RemoveCommand((sender as HyperlinkButton).CommandParameter as string);

	void RemoveCommand(string text) {
		for (int i = 0; i < VM.VoiceCommands.Count; i++) {
			if (string.Equals(text, VM.VoiceCommands[i].Text, StringComparison.CurrentCultureIgnoreCase)) {
				VM.VoiceCommands.RemoveAt(i);
				return;
			}
		}
	}

	async void Save_Click(object sender, RoutedEventArgs e) {
		try {
			VM.IsLoading = true;

			VoiceCommandProfile profile = CreateProfile();

			File.WriteAllText(VoiceCommandProfile.GetFilePath(VM.EXEName),
				JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true }));

			AppUserSettings.EnableVoiceCommands = true;  // in case it was switched off

			VM.IsLoading = false;

			Frame.GoBack();
		} catch (Exception ex) {
			await VM.HandleExceptionAsync(this.XamlRoot, ex, "Save voice profile error");
		}
	}

	VoiceCommandProfile CreateProfile() {
		if (VM.SelectedLanguage == null) throw new Exception("Please select a language");
		if (VM.VoiceCommands.Count == 0 && string.IsNullOrWhiteSpace(VM.InjectText))
			throw new Exception("Please add at least one voice command");

		var profile = new VoiceCommandProfile {
			EXEName = VM.EXEName,
			MinConfidence = (float)(slMinConfidence.Value / 100f),
			KeyPressDelayMS = VM.KeyPressDelayMS,
			InjectText = VM.InjectText?.Trim(),
			LanguageTag = VM.SelectedLanguage.LanguageTag,
			Commands = [.. VM.VoiceCommands.Select(c => new VoiceCommand {
				Text = c.Text,
				VKKeyCodes = c.VKKeyCodes
			})]
		};
		return profile;
	}

	#region * Test
	VoiceCommandRecognizer testRecognizer;
	DispatcherTimer overlayTimer;

	/// <summary>Opens the test overlay and starts the voice command recognizer for testing</summary>
	async void Test_Click(object sender, RoutedEventArgs e) {
		const string IDLE_TEXT = "( Listening... )";

		try {
			var profile = CreateProfile();

			// Overlay timer to hide the text after 
			if (overlayTimer == null) {
				overlayTimer = new() { Interval = TimeSpan.FromSeconds(2) };
				overlayTimer.Tick += (s, args) => {
					overlayTimer.Stop();
					tbRecognizedText.Text = IDLE_TEXT;
				};
			}

			// Show the overlay
			borTestOverlay.Visibility = Visibility.Visible;
			tbRecognizedText.Text = IDLE_TEXT;

			// Create and start the test recognizer
			testRecognizer = new VoiceCommandRecognizer();
			testRecognizer.SpeechRecognized += TestRecognizer_SpeechRecognized;
			testRecognizer.Start(profile);
		} catch (Exception ex) {
			await VM.HandleExceptionAsync(this.XamlRoot, ex, "Test Voice Commands Error");
		}
	}

	/// <summary>Handles speech recognition events during testing</summary>
	void TestRecognizer_SpeechRecognized(object sender, string recognizedText, float confidence)
		=> DispatcherQueue.TryEnqueue(() => {
			tbRecognizedText.Text = $"{recognizedText} [{confidence:P0}]";
			overlayTimer.Stop();
			overlayTimer.Start();
		});

	/// <summary>Closes the test overlay and stops the recognizer</summary>
	void CloseTestOverlay_Click(object sender, RoutedEventArgs e) {
		StopTestRecognizer();
		borTestOverlay.Visibility = Visibility.Collapsed;
	}

	/// <summary>Stops the test recognizer and cleans up resources</summary>
	void StopTestRecognizer() {
		if (testRecognizer != null) {
			testRecognizer.SpeechRecognized -= TestRecognizer_SpeechRecognized;
			testRecognizer.Stop();
			testRecognizer = null;
		}

		overlayTimer?.Stop(); overlayTimer = null;
	}
	#endregion

	void Back_Click(object sender, RoutedEventArgs e) => Frame.GoBack();
}
