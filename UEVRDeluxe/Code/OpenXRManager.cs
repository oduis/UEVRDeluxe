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
	const string ENV_XR_RUNTIME_JSON = "XR_RUNTIME_JSON";

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
		string activeRuntimePath = keyOpenXRRoot?.GetValue(REGKEY_NAME_ACTIVE_RUNTIME)?.ToString();

		// Fallback: No active Runtime set in registry, so take env variable
		if (string.IsNullOrWhiteSpace(activeRuntimePath)) activeRuntimePath = Environment.GetEnvironmentVariable(ENV_XR_RUNTIME_JSON);

		// Read all that are available
		var keyRuntimes = hklm.OpenSubKey(REGKEY_ALL_RUNTIMES, false);
		var availablePaths = keyRuntimes?.GetValueNames().Where(v => File.Exists(v)) ?? Enumerable.Empty<string>();

		foreach (var path in availablePaths) {
			runtimes.Add(new OpenXRRuntime {
				Path = path,
				Name = GetRuntimeName(path),
				IsDefault = string.Equals(path, activeRuntimePath, StringComparison.OrdinalIgnoreCase)
			});
		}

		// Include the active runtime from the environment variable if it's not listed under AvailableRuntimes
		if (!string.IsNullOrWhiteSpace(activeRuntimePath) && File.Exists(activeRuntimePath)
			&& !runtimes.Any(r => string.Equals(r.Path, activeRuntimePath, StringComparison.OrdinalIgnoreCase))) {
			runtimes.Add(new OpenXRRuntime { Path = activeRuntimePath, Name = GetRuntimeName(activeRuntimePath), IsDefault = true });
		}

		return runtimes;
	}

	/// <summary>Display name of a runtime manifest</summary>
	static string GetRuntimeName(string path) {
		if (MAP_FILENAME2DISPLAYNAME.TryGetValue(Path.GetFileName(path), out var displayName)) return displayName;

		// Runtime points to a JSON file
		string json = File.ReadAllText(path);
		var jsonDoc = JsonDocument.Parse(json);
		var root = jsonDoc.RootElement;

		if (root.TryGetProperty("runtime", out var runtimeElement) && runtimeElement.TryGetProperty("name", out var name)) {
			return name.GetString();
		}

		// Should not happen...
		return Path.GetFileNameWithoutExtension(path);
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
