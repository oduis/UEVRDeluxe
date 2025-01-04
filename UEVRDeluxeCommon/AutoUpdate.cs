using System.Text.Json.Serialization;

namespace UEVRDeluxe.Common;

/// <summary>Metadata of a game profile</summary>
public class Autoupdate {
	public const string FILENAME = "Autoupdate.json";

	/// <summary>For future expansion. Currently UEVRDeluxe</summary>
	[JsonPropertyName("app")]
	public string App { get; set; }

	/// <summary>Current version of the app. Format 1.0.0.0</summary>
	[JsonPropertyName("version")]
	public string Version { get; set; }

	/// <summary>Release notes, short version</summary>
	[JsonPropertyName("releaseNotes")]
	public string ReleaseNotes { get; set; }

	// Can't use direct EXE downloads right now.
	// UEVR DLLs are often marked as virus, so the user has to trust it in the browser

	/// <summary>URL to the web page</summary>
	[JsonPropertyName("webURL")]
	public string WebURL { get; set; }
}
