#region Usings
using System;
using System.IO;
#endregion

namespace UEVRDeluxe.Code;

/// <summary>Helper to create shortcuts (.lnk) that launch this app with arguments (for example a game id).
/// Uses Windows Script Host COM (WshShell) for simplicity and compatibility in unpackaged WinUI apps.</summary>
public sealed class ShortcutHandler {
	string targetExePath, arguments, shortcutName;

	public ShortcutHandler(string targetExePath, string arguments, string shortcutName) {
		if (string.IsNullOrWhiteSpace(targetExePath)) throw new ArgumentException(nameof(targetExePath));
		if (string.IsNullOrWhiteSpace(shortcutName)) throw new ArgumentException(nameof(shortcutName));
		if (!File.Exists(targetExePath)) throw new FileNotFoundException("Target exe not found", targetExePath);

		this.targetExePath = targetExePath;
		this.arguments = arguments ?? string.Empty;
		this.shortcutName = shortcutName;
	}

	string GetDesktopFolder() => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
	string GetShortcutPath() => Path.Combine(GetDesktopFolder(), shortcutName + ".lnk");

	Type GetWshShellType() => Type.GetTypeFromProgID("WScript.Shell");

	/// <summary>
	/// Returns true when the desktop shortcut exists and matches the handler's TargetExePath and Arguments.
	/// </summary>
	public bool GetExists() {
		string path = GetShortcutPath();
		if (!File.Exists(path)) return false;

		Type wshType = GetWshShellType();
		if (wshType == null) return false;

		dynamic shell = Activator.CreateInstance(wshType)!;
		dynamic shortcut = shell.CreateShortcut(path);

		string existingTarget = (shortcut.TargetPath as string) ?? string.Empty;
		string existingArgs = (shortcut.Arguments as string) ?? string.Empty;

		return string.Equals(existingTarget, targetExePath, StringComparison.OrdinalIgnoreCase)
			&& string.Equals(existingArgs, arguments ?? string.Empty, StringComparison.Ordinal);
	}

	/// <summary>
	/// Creates or replaces the desktop shortcut using the handler's TargetExePath, Arguments and ShortcutName.
	/// Optional description and icon parameters can be provided per-call.
	/// </summary>
	public void CreateOrReplace(string description = null, string iconPath = null, int iconIndex = 0) {
		if (!File.Exists(targetExePath)) throw new FileNotFoundException("Target exe not found", targetExePath);

		string folder = GetDesktopFolder();

		string shortcutPath = GetShortcutPath();

		Type wshType = GetWshShellType()
			?? throw new Exception("Cannot find Script shell");

		dynamic shell = Activator.CreateInstance(wshType)!;
		dynamic shortcut = shell.CreateShortcut(shortcutPath);

		shortcut.TargetPath = targetExePath;
		shortcut.WorkingDirectory = Path.GetDirectoryName(targetExePath);
		shortcut.Arguments = arguments ?? string.Empty;

		if (!string.IsNullOrEmpty(description)) shortcut.Description = description;
		if (!string.IsNullOrEmpty(iconPath)) shortcut.IconLocation = iconPath + "," + iconIndex.ToString();

		else shortcut.IconLocation = targetExePath + ",0";

		shortcut.Save();
	}

	/// <summary>
	/// Deletes the desktop shortcut represented by this handler.
	/// </summary>
	public bool Delete() {
		string path = GetShortcutPath();
		if (!File.Exists(path)) return false;
		try {
			File.Delete(path);
			return true;
		} catch {
			return false;
		}
	}
}
