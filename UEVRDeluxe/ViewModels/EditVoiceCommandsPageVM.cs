#region Usings
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UEVRDeluxe.Code;
using UEVRDeluxe.Common;
using Windows.Globalization;
#endregion

namespace UEVRDeluxe.ViewModels;

public class EditVoiceCommandsPageVM : VMBase {
	public string EXEName;

	string text, textKeyCode;
	public string Text { get => text; set => Set(ref text, value); }
	public string TextKeyCode { get => textKeyCode; set => Set(ref textKeyCode, value); }

	List<Language> languages;
	public List<Language> Languages { get => languages; set => Set(ref languages, value); }

	Language selectedLanguage;
	public Language SelectedLanguage { get => selectedLanguage; set => Set(ref selectedLanguage, value); }

	string injectText;
	public string InjectText { get => injectText; set => Set(ref injectText, value); }

	int keyPressDelayMS = VoiceCommandProfile.DEFAULT_KEYPRESS_DELAY_MS;
	public int KeyPressDelayMS { get => keyPressDelayMS; set => Set(ref keyPressDelayMS, value); }

	ObservableCollection<VoiceCommandEx> voiceCommands;
	public ObservableCollection<VoiceCommandEx> VoiceCommands { get => voiceCommands; set => Set(ref voiceCommands, value); }
}

public class VoiceCommandEx : VoiceCommand {
	/// <summary>Display text for keyboard keys (e.g. "M Tab F1"), used only in UI</summary>
	public string TextKeyCode { get; set; }

	public VoiceCommandEx() { }

	/// <summary>Anything above on char cannot be mapped using Win32</summary>
	static readonly Dictionary<string, int> MAP_KEYNAME_VKKEYCODE = new(StringComparer.OrdinalIgnoreCase) {
		{ "F1", 0x70 }, { "F2", 0x71 }, { "F3", 0x72 }, { "F4", 0x73 },
		{ "F5", 0x74 }, { "F6", 0x75 }, { "F7", 0x76 }, { "F8", 0x77 },
		{ "F9", 0x78 }, { "F10", 0x79 }, { "F11", 0x7A }, { "F12", 0x7B },
		{ "Enter", 0x0D }, { "Escape", 0x1B }, { "Esc", 0x1B }, { "Space", 0x20 }, { "Tab", 0x09 },
		{ "Left", 0x25 }, { "Up", 0x26 }, { "Right", 0x27 }, { "Down", 0x28 },
		{ "PgUp", 0x21 }, { "PgDown", 0x22 }, { "Ins", 0x2D }, { "Del", 0x2E },
		{ "Home", 0x24 }, { "End", 0x23 }, { "Ctrl", 0x11 }, { "Shift", 0x10 }
	};

	public VoiceCommandEx(VoiceCommand cmd) {
		if (cmd == null) return;
		this.Text = cmd.Text;
		this.VKKeyCodes = cmd.VKKeyCodes;
		this.TextKeyCode = string.Empty;

		if (cmd.VKKeyCodes?.Length > 0) {

			var displayTexts = new List<string>();

			foreach (var vkCode in cmd.VKKeyCodes) {
				var dict = MAP_KEYNAME_VKKEYCODE.Where(v => v.Value == vkCode);

				if (dict.Any()) {
					displayTexts.Add(dict.First().Key);
				} else {
					displayTexts.Add(((char)vkCode).ToString());
				}
			}

			this.TextKeyCode= string.Join(" ", displayTexts);
		}
	}
}