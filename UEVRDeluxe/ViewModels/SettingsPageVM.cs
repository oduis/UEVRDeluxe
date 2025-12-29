#region Usings
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;
using UEVRDeluxe.Code;
#endregion

namespace UEVRDeluxe.ViewModels;

public class SettingsPageVM : VMBase {
	#region OpenXR Runtime
	List<OpenXRRuntime> openXRRuntimes;
	public List<OpenXRRuntime> OpenXRRuntimes { get => openXRRuntimes; set => Set(ref openXRRuntimes, value, [nameof(OpenXRRuntimeVisible)]); }

	OpenXRRuntime selectedRuntime;
	public OpenXRRuntime SelectedRuntime { get => selectedRuntime; set => Set(ref selectedRuntime, value); }

	/// <summary>There are some special runtimes liek WMR und Varjo that don't play nice. Leave them alone.</summary>
	public Visibility OpenXRRuntimeVisible => (openXRRuntimes?.Count(r => r.IsDefault) ?? 0) == 1 ? Visibility.Visible : Visibility.Collapsed;
	#endregion

	#region Virtual Desktop calculator
	double resolutionFactor = 1.0;
	public double ResolutionFactor { get => resolutionFactor; set => Set(ref resolutionFactor, value, new[] { nameof(RenderedResolutionText), nameof(UpscaledResolutionText) }); }

	public Windows.Globalization.NumberFormatting.DecimalFormatter DecimalFormatter { get; } = new Windows.Globalization.NumberFormatting.DecimalFormatter() {
		IntegerDigits = 1,
		FractionDigits = 2
	};

	public VRHeadset[] VRHeadsets => VR_HEADSETS;

	VRHeadset selectedVRHeadset = VR_HEADSETS.First(h => h.Name == "Meta Quest 3");
	public VRHeadset SelectedVRHeadset {
		get => selectedVRHeadset;
		set => Set(ref selectedVRHeadset, value, new[] { nameof(RenderedResolutionText), nameof(UpscaledResolutionText), nameof(StreamedResolutionText), nameof(ResolutionHelp) });
	}


	public VDGraphicsPreset[] VDGraphicsPresets => VD_GRAPHICS_PRESETS;

	VDGraphicsPreset selectedVDGraphicsPreset = VD_GRAPHICS_PRESETS.First(p => p.Name == "Ultra");
	public VDGraphicsPreset SelectedVDGraphicsPreset {
		get => selectedVDGraphicsPreset;
		set => Set(ref selectedVDGraphicsPreset, value, new[] { nameof(RenderedResolutionText), nameof(UpscaledResolutionText), nameof(StreamedResolutionText) });
	}


	public DLSSSetting[] DLSS4Settings => DLSS_4_SETTINGS;

	DLSSSetting selectedDLSS4Setting = DLSS_4_SETTINGS.First(s => s.Name == "Balanced");
	public DLSSSetting SelectedDLSS4Setting {
		get => selectedDLSS4Setting;
		set => Set(ref selectedDLSS4Setting, value, new[] { nameof(RenderedResolutionText), nameof(UpscaledResolutionText), nameof(StreamedResolutionText) });
	}

	public string RenderedResolutionText {
		get {
			if (!SelectedVDGraphicsPreset.ResolutionByHeadsetName.TryGetValue(SelectedVRHeadset.Name, out Resolution res))
				return "( Invalid VD setting )";

			var renderedRes = new Resolution(
				(int)(res.Width * ResolutionFactor * SelectedDLSS4Setting.ResolutionFactor),
			   (int)(res.Height * ResolutionFactor * SelectedDLSS4Setting.ResolutionFactor));

			return SelectedVRHeadset.ResolutionText(renderedRes);
		}
	}

	public string UpscaledResolutionText {
		get {
			if (!SelectedVDGraphicsPreset.ResolutionByHeadsetName.TryGetValue(SelectedVRHeadset.Name, out Resolution res))
				return "( Invalid VD setting )";

			var renderedRes = new Resolution(
				(int)(res.Width * ResolutionFactor),
			   (int)(res.Height * ResolutionFactor));

			return SelectedVRHeadset.ResolutionText(renderedRes);
		}
	}

	public string StreamedResolutionText {
		get {
			if (!SelectedVDGraphicsPreset.ResolutionByHeadsetName.TryGetValue(SelectedVRHeadset.Name, out Resolution res))
				return "( Invalid VD setting )";

			var renderedRes = new Resolution(res.Width, res.Height);
			return SelectedVRHeadset.ResolutionText(renderedRes);
		}
	}

	public string ResolutionHelp
		=> $"The {selectedVRHeadset.Name} has a panel resolution of {selectedVRHeadset.PanelResolution.Width}x{selectedVRHeadset.PanelResolution.Height}, "
			+ "but you need a higher resolution than panel (around 1.5x) to compensate for pixels lost from optical distortions.\n"
			+ "You can change the default resolution factor (1.0) either in UEVR or in the Virtual Desktop's Advanced tab, but it may look blurry.";
	#endregion

	#region * VD/VR constants
	public static readonly VRHeadset[] VR_HEADSETS = [
		new() { Name = "Meta Quest", PanelResolution = new(1440, 1600) },
		new() { Name = "Meta Quest 2", PanelResolution = new(1832, 1920) },
		new() { Name = "Meta Quest Pro", PanelResolution = new(1800, 1920) },
		new() { Name = "Meta Quest 3", PanelResolution = new(2064, 2208) },
		new() { Name = "Meta Quest 3S", PanelResolution = new(1832, 1920) },

		// Correction factors currently unknown for these two headsets
		new() { Name = "Galaxy XR", PanelResolution = new(3552, 3840) },
		new() { Name = "Play For Dream", PanelResolution = new(3840, 3552) },

		new() { Name = "PICO 4", PanelResolution = new(2160, 2160) },
		new() { Name = "PICO 4 Ultra", PanelResolution = new(2160, 2160) },
		new() { Name = "PICO Neo 3 Link", PanelResolution = new(1832, 1920) },

		new() { Name = "HTC Vive Focus 3", PanelResolution = new(2448, 2448) },
		new() { Name = "HTC Vive XR Elite", PanelResolution = new(1920, 1920) },

		new() { Name = "Valve Index", PanelResolution = new(1440, 1600) },
		new() { Name = "HTC Vive", PanelResolution = new(1080, 1200) },
		new() { Name = "HTC Vive Pro", PanelResolution = new(1440, 1600) },
		new() { Name = "HTC Vive Pro 2", PanelResolution = new(2448, 2448) },

		new() { Name = "HP Reverb G2", PanelResolution = new(2160, 2160) },
		new() { Name = "Pimax Crystal Light", PanelResolution = new(2880, 2880) },
		new() { Name = "Pimax Crystal Super", PanelResolution = new(3840, 3840) },
		new() { Name = "Pimax 8K X/Plus", PanelResolution = new(3840, 2160) },
		new() { Name = "Bigscreen Beyond", PanelResolution = new(2560, 2560) },
		new() { Name = "Varjo Aero", PanelResolution = new(2880, 2720) },
	];


	public static readonly VDGraphicsPreset[] VD_GRAPHICS_PRESETS = [
		new("Potato", new Dictionary<string, Resolution> {
			["Meta Quest"] = new(1200, 1344),
			["Meta Quest 2"] = new(1344, 1440),
			["Meta Quest 3S"] = new(1344, 1440),
			["PICO Neo 3 Link"] = new(1344, 1440),
			["Meta Quest Pro"] = new(1344, 1440),
			["Meta Quest 3"] = new(1344, 1440),
			["Galaxy XR"] = new(1344, 1440),
			["HTC Vive XR Elite"] = new(1344, 1344),
			["PICO 4"] = new(1344, 1344),
			["PICO 4 Ultra"] = new(1344, 1344),
			["Play For Dream"] = new(1536, 1440),
		}),
		new("Low", new Dictionary<string, Resolution> {
			// Quest
			["Meta Quest"] = new(1536, 1728),
			// Quest 2/Quest 3S/Pico Neo 3/Quest Pro/Quest 3/Galaxy XR
			["Meta Quest 2"] = new(1728, 1824),
			["Meta Quest 3S"] = new(1728, 1824),
			["PICO Neo 3 Link"] = new(1728, 1824),
			["Meta Quest Pro"] = new(1728, 1824),
			["Meta Quest 3"] = new(1728, 1824),
			["Galaxy XR"] = new(1728, 1824),
			// Vive XR Elite/Pico 4 / Pico 4 Ultra
			["HTC Vive XR Elite"] = new(1728, 1728),
			["PICO 4"] = new(1728, 1728),
			["PICO 4 Ultra"] = new(1728, 1728),
			// Play For Dream
			["Play For Dream"] = new(1824, 1728),
		}),
		new("Medium", new Dictionary<string, Resolution> {
			// Quest
			["Meta Quest"] = new(1824, 2016),
			// Quest 2/Quest 3S/Pico Neo 3/Quest Pro/Quest 3/Galaxy XR
			["Meta Quest 2"] = new(2112, 2304),
			["Meta Quest 3S"] = new(2112, 2304),
			["PICO Neo 3 Link"] = new(2112, 2304),
			["Meta Quest Pro"] = new(2112, 2304),
			["Meta Quest 3"] = new(2112, 2304),
			["Galaxy XR"] = new(2112, 2304),
			// Vive XR Elite/Pico 4 / Pico 4 Ultra
			["HTC Vive XR Elite"] = new(2016, 2016),
			["PICO 4"] = new(2016, 2016),
			["PICO 4 Ultra"] = new(2016, 2016),
			// Play For Dream
			["Play For Dream"] = new(2208, 2016),
		}),
		new("High", new Dictionary<string, Resolution> {
			// Quest
			["Meta Quest"] = new(2208, 2400),
			// Quest 2/Quest 3S/Pico Neo 3/Quest Pro/Quest 3/Galaxy XR
			["Meta Quest 2"] = new(2496, 2688),
			["Meta Quest 3S"] = new(2496, 2688),
			["PICO Neo 3 Link"] = new(2496, 2688),
			["Meta Quest Pro"] = new(2496, 2688),
			["Meta Quest 3"] = new(2496, 2688),
			["Galaxy XR"] = new(2496, 2688),
			// Vive XR Elite/Pico 4 / Pico 4 Ultra
			["HTC Vive XR Elite"] = new(2592, 2592),
			["PICO 4"] = new(2592, 2592),
			["PICO 4 Ultra"] = new(2592, 2592),
			// Play For Dream
			["Play For Dream"] = new(2688, 2496),
		}),
		new("Ultra", new Dictionary<string, Resolution> {
			// Quest 2/Quest 3S/Pico Neo 3/Quest Pro/Quest 3/Galaxy XR
			["Meta Quest 2"] = new(2688, 2880),
			["Meta Quest 3S"] = new(2688, 2880),
			["PICO Neo 3 Link"] = new(2688, 2880),
			["Meta Quest Pro"] = new(2688, 2880),
			["Meta Quest 3"] = new(2688, 2880),
			["Galaxy XR"] = new(2688, 2880),
			// Vive XR Elite/Pico 4 / Pico 4 Ultra
			["HTC Vive XR Elite"] = new(2784, 2784),
			["PICO 4"] = new(2784, 2784),
			["PICO 4 Ultra"] = new(2784, 2784),
			// Play For Dream
			["Play For Dream"] = new(2880, 2688),
		}),
		new("Godlike", new Dictionary<string, Resolution> {
			// Quest 2/Quest Pro/Quest 3/Galaxy XR
			["Meta Quest 2"] = new(3072, 3264),
			["Meta Quest Pro"] = new(3072, 3264),
			["Meta Quest 3"] = new(3072, 3264),
			["Galaxy XR"] = new(3072, 3264),
			// Vive XR Elite/Pico 4 / Pico 4 Ultra
			["HTC Vive XR Elite"] = new(3168, 3168),
			["PICO 4"] = new(3168, 3168),
			["PICO 4 Ultra"] = new(3168, 3168),
			// Play For Dream
			["Play For Dream"] = new(3264, 3072),
		}),
		new("Monster", new Dictionary<string, Resolution> {
			// Galaxy XR
			["Galaxy XR"] = new(3648, 3936),
			// Vive XR Elite/Pico 4 / Pico 4 Ultra
			["PICO 4 Ultra"] = new(3648, 3648),
			// Play For Dream
			["Play For Dream"] = new(3936, 3648),
		}),
	];

	public static readonly DLSSSetting[] DLSS_4_SETTINGS = [
		new() { Name = "Off or DLAA", ResolutionFactor = 1.0 },
		new() { Name = "Quality", ResolutionFactor = 0.666666 },
		new() { Name = "Balanced", ResolutionFactor = 0.58 },
		new() { Name = "Performance", ResolutionFactor = 0.5 },
		new() { Name = "Ultra Performance", ResolutionFactor = 0.33333 }
	];
	#endregion

	#region * Other properties
	int delayBeforeInjection;
	public int DelayBeforeInjection { get => delayBeforeInjection; set => Set(ref delayBeforeInjection, value); }
	#endregion
}

public readonly record struct Resolution(int Width, int Height);

public class DLSSSetting {
	public string Name { get; set; }
	public double ResolutionFactor { get; set; }
}

public class VRHeadset {
	public string Name { get; set; }
	public Resolution PanelResolution { get; set; }

	public string ResolutionText(Resolution res)
		=> $"{res.Width} x {res.Height} pixels\n"
		+ $"{(res.Width * res.Height / 1_000_000f):F1} megapixels\n"
		+ $"{((res.Width / (float)PanelResolution.Width + res.Height / (float)PanelResolution.Height) * 0.5f):N2}x panel resolution";
}

/// <summary>Virtual Desktop graphics preset</summary>
public class VDGraphicsPreset {
	public VDGraphicsPreset(string name, Dictionary<string, Resolution> resolutionByHeadsetName) {
		Name = name; ResolutionByHeadsetName = resolutionByHeadsetName;
	}

	public string Name { get; }

	public Dictionary<string, Resolution> ResolutionByHeadsetName { get; }
}
