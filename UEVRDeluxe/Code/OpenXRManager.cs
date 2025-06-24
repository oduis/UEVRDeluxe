#region Usings
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
#endregion

namespace UEVRDeluxe.Code;
public static class OpenXRManager {

	const string REGKEY_OPENXRV1ROOT = @"SOFTWARE\Khronos\OpenXR\1";
	const string REGKEY_ALL_RUNTIMES = REGKEY_OPENXRV1ROOT + @"\AvailableRuntimes";
	const string REGKEY_IMPLICITLAYERS = REGKEY_OPENXRV1ROOT + @"\ApiLayers\Implicit";
	const string REGKEY_NAME_ACTIVE_RUNTIME = "ActiveRuntime";

	/// <summary>Some have old name the user might not recognize.</summary>
	readonly static Dictionary<string, string> MAP_FILENAME2DISPLAYNAME = new() {
		{ "oculus_openxr_64.json", "Meta Quest Link" }
	};

	/// <summary>Search runtimes and defaults</summary>
	/// <returns>All runtimes</returns>
	/// <remarks>Some runtimes like the old Windows Mixed Reality and Varjo don't play nice, but they are very niche, so ignore</remarks>
	public static List<OpenXRRuntime> GetAllRuntimes() {
		var runtimes = new List<OpenXRRuntime>();

		using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);

		// Get the current runtime
		var keyOpenXRRoot = hklm.OpenSubKey(REGKEY_OPENXRV1ROOT, false);
		if (keyOpenXRRoot == null) return runtimes;

		string activeRuntimePath = keyOpenXRRoot.GetValue(REGKEY_NAME_ACTIVE_RUNTIME)?.ToString();

		// Read all that are available
		var keyRuntimes = hklm.OpenSubKey(REGKEY_ALL_RUNTIMES, false);
		if (keyRuntimes == null) return runtimes;

		foreach (var path in keyRuntimes.GetValueNames().Where(v => File.Exists(v))) {
			var runtime = new OpenXRRuntime { Path = path };

			if (string.Equals(path, activeRuntimePath, StringComparison.OrdinalIgnoreCase)) {
				runtime.IsDefault = true;
			}

			if (MAP_FILENAME2DISPLAYNAME.TryGetValue(Path.GetFileName(path), out var displayName)) {
				runtime.Name = displayName;
			} else {
				// Runtime points to a JSON file
				var json = File.ReadAllText(path);
				var jsonDoc = JsonDocument.Parse(json);
				var root = jsonDoc.RootElement;

				if (root.TryGetProperty("runtime", out var runtimeElement) && runtimeElement.TryGetProperty("name", out var name)) {
					runtime.Name = name.GetString();
				} else {
					// Should not happen...
					runtime.Name = Path.GetFileNameWithoutExtension(path);
				}
			}

			runtimes.Add(runtime);
		}

		return runtimes;
	}

	public static void SetActiveRuntime(string path) {
		using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);

		var keyOpenXRRoot = hklm.OpenSubKey(REGKEY_OPENXRV1ROOT, true);
		if (keyOpenXRRoot == null) keyOpenXRRoot = hklm.CreateSubKey(REGKEY_OPENXRV1ROOT);

		string currentPath = keyOpenXRRoot.GetValue(REGKEY_NAME_ACTIVE_RUNTIME)?.ToString() ?? string.Empty;
		if (string.Equals(path, currentPath, StringComparison.OrdinalIgnoreCase)) return;

		keyOpenXRRoot.SetValue(REGKEY_NAME_ACTIVE_RUNTIME, path);
	}

	/// <summary>This is outdated since 2023 and is know to causes trouble in newer games</summary>
	public static bool IsOpenXRToolkitEnabled() {
		using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
		var layers = hklm.OpenSubKey(REGKEY_IMPLICITLAYERS, false);

		// enumerate all keys in layers. if the Name contains XR_APILAYER_MBUCCHIA and the dword value is 1, return true.
		if (layers == null) return false;
		string keyOpenXRToolkit = layers.GetValueNames().FirstOrDefault(name => name.Contains("XR_APILAYER_MBUCCHIA", StringComparison.OrdinalIgnoreCase));
		if (keyOpenXRToolkit == null) return false;
		var value = layers.GetValue(keyOpenXRToolkit);
		if (value is not int dwordValue) return false;
		return dwordValue == 0;  // if it is 1 its installed, but disabled
	}
}

public class OpenXRRuntime {
	public string Name { get; set; }
	public string Path { get; set; }

	public bool IsDefault { get; set; }
}
