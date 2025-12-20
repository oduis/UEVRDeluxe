#region Usings
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

	ObservableCollection<VoiceCommandEx> voiceCommands;
	public ObservableCollection<VoiceCommandEx> VoiceCommands { get => voiceCommands; set => Set(ref voiceCommands, value); }
}

public class VoiceCommandEx : VoiceCommand {
	/// <summary>One char or e.g. F11 for function key 11</summary>
	public string TextKeyCode { get; set; }
}