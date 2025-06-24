using Microsoft.Win32;
using System.Collections.Generic;
using System.Management;

namespace UEVRDeluxe.Code;

public static class SystemInfo {
	const string REGKEY_GRAPHICS = @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers";
	const string REGKEY_NAME_SCHEDULER = "HwSchMode";

	public static bool IsHardwareSchedulingEnabled() {
		using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);

		var keyOpenXRRoot = hklm.OpenSubKey(REGKEY_GRAPHICS, false);
		if (keyOpenXRRoot != null) {
			string hwSchMode = keyOpenXRRoot.GetValue(REGKEY_NAME_SCHEDULER)?.ToString();
			return hwSchMode == "2";
		}

		return false;
	}

	public static List<string> GetInstalledGPUs() {
		var gpus = new List<string>();

		using (var searcher = new ManagementObjectSearcher("select * from Win32_VideoController")) {
			foreach (var obj in searcher.Get()) {
				var name = obj["Name"]?.ToString();

				// Remove e.g. Virtual Desktop
				var isPhysical = obj["PNPDeviceID"]?.ToString()?.StartsWith("PCI\\") == true;

				if (!string.IsNullOrEmpty(name) && isPhysical) gpus.Add(name);
			}
		}

		return gpus;
	}
}
