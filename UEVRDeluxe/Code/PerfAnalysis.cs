using System.Diagnostics;
using System.Management;
using System.Collections.Generic;
using System;
using System.Linq;

namespace UEVRDeluxe.Code;

public class PerfAnalyzer {

	/// <summary>
	/// Gets indices of Nvidia GPU adapters using WMI.
	/// </summary>
	static int FindNVIDIAAdapterIndex() {
		try {
			using var searcher = new ManagementObjectSearcher("select * from Win32_VideoController");
			int idx = 0;
			foreach (ManagementObject obj in searcher.Get()) {
				string name = obj["Name"]?.ToString() ?? "";
				string manufacturer = obj["AdapterCompatibility"]?.ToString() ?? "";
				if (manufacturer.Contains("NVidia", StringComparison.OrdinalIgnoreCase)
					|| name.Contains("NVidia", StringComparison.OrdinalIgnoreCase))
					return idx;

				idx++;
			}
		} catch (Exception ex) {
			Trace.WriteLine($"WMI error: {ex.Message}");
		}

		return -1;
	}

	public void Initialize() {

		int nVidiaAdapterIndex = FindNVIDIAAdapterIndex();
		if (nVidiaAdapterIndex < 0) throw new Exception("No NVidia GPU found (feature is currently NVidia only)");

		var category = new PerformanceCounterCategory("GPU Engine");
		string[] instances = category.GetInstanceNames().Where(i => i.Contains($"_phys_{nVidiaAdapterIndex}_")).ToArray();

		Trace.WriteLine("Enumerating GPU Engine instances...");
		foreach (var instance in instances) {
			if (instance.Contains("engtype_VideoEncode")) {
				try {
					using (var counter = new PerformanceCounter("GPU Engine", "Utilization Percentage", instance)) {
						float value1 = counter.NextValue();
						System.Threading.Thread.Sleep(1000);
						float value2 = counter.NextValue();
						Trace.WriteLine($"{instance} : {value2:F1} %");
					}
				} catch (Exception ex) {
					Trace.WriteLine($"PerfCounter error: {ex.Message}");
				}
			}

		}
	}

}