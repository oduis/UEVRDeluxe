using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace UEVRDeluxe.Code;

/// <summary>This JSON is loaded from the server and takes tweaks in settings for the GameStoreManager.</summary>
public class CustomizingSettings {
	/// <summary>These EXE Names might be launchers next to Shipping.. files. These launcher EXEs are the injection points.</summary>
	/// <remarks>Example: Star Wars Fallen Order</remarks>
	[JsonPropertyName("exeNamesLauncher")]
	public List<string> EXENameLauncher { get; set; }

	/// <summary>Some parts of e.g. MODS and tools that might interfer</summary>
	[JsonPropertyName("exeNamePartsToIgnore")]
	public List<string> EXENamePartsToIgnore { get; set; }
}
