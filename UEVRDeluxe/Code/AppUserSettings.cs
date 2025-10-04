#region Usings
using IniParser.Model;
using System;
using System.IO;
#endregion

namespace UEVRDeluxe.Code;

/// <summary>Simple Ini file in user directory, since ApplicationDataContainer only works for packaged WinUI3 apps</summary>
public static class AppUserSettings {

	#region * Generic access
	static IniData settings = null;

	static string GetINIFileName() => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\UEVRDeluxe\\UserSettings.ini";

	public static string Read(string key) {
		InitSettings();
		return settings.Global[key];
	}

	public static void Write(string key, string value) {
		InitSettings();

		settings.Global[key] = value;
		var parser = new IniParser.FileIniDataParser();
		parser.WriteFile(GetINIFileName(), settings);
	}

	static void InitSettings() {
		if (settings == null) {
			var parser = new IniParser.FileIniDataParser();

			string iniFileName = GetINIFileName();
			if (File.Exists(iniFileName))
				settings = parser.ReadFile(iniFileName);
			else
				settings = new IniData();
		}
	}
	#endregion

	#region * Specialized properties
	// This property is not set, but has a default value till it is overwritten
	// Allows for dynamisch changes afterwards
	public const int DEFAULT_DELAY_BEFORE_INJECTION_SEC = 3;

	public static int GetDelayBeforeInjectionSec() {
		int delayBeforeInjectionSec = AppUserSettings.DEFAULT_DELAY_BEFORE_INJECTION_SEC;
		string appSetting = AppUserSettings.Read("DelayBeforeInjectionSec");
		if (int.TryParse(appSetting, out int iAppSetting) && iAppSetting > 0) delayBeforeInjectionSec = iAppSetting;
		return delayBeforeInjectionSec;
	}

	const string KEY_ENABLE_VOICE_COMMANDS = "EnableVoiceCommands";

	public static bool EnableVoiceCommands {
		get {
			string appSetting = Read(KEY_ENABLE_VOICE_COMMANDS);
			if (appSetting != null && bool.TryParse(appSetting, out bool value)) return value;
			return true;
		}
		set {
			Write(KEY_ENABLE_VOICE_COMMANDS, value.ToString());
		}
	}
	#endregion
}