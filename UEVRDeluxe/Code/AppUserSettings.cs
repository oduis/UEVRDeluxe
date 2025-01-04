#region Usings
using IniParser.Model;
using System;
using System.IO; 
#endregion

namespace UEVRDeluxe.Code;

/// <summary>Simple Ini file in user directory, since ApplicationDataContainer only works for packaged WinUI3 apps</summary>
public static class AppUserSettings {
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
}