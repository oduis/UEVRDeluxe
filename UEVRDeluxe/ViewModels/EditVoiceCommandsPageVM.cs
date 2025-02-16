#region Usings
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UEVRDeluxe.Common;
using Windows.Globalization;
#endregion

namespace UEVRDeluxe.ViewModels;

public class EditVoiceCommandsPageVM : VMBase {
	public string EXEName;

	List<Language> languages;
	public List<Language> Languages { get => languages; set => Set(ref languages, value); }

	Language selectedLanguage;
	public Language SelectedLanguage { get => selectedLanguage; set => Set(ref selectedLanguage, value); }
}

