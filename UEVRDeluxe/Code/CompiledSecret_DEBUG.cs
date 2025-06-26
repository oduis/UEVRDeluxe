namespace UEVRDeluxe.Code;

#if DEBUG
/// <summary>To have compiled in secrets, the CSPROJ has references to multiple files, depending on the environment.</summary>
/// <remarks>the CompiledSecret for RELEASE is not checked into Github</remarks>
public static class CompiledSecret {
	public const string FUNCTION_BASE_ADDRESS = "http://localhost:7299/api/";

	/// <summary>In RELEASE the URL to the autoupdate file. Ignored if empty.</summary>
	public const string AUTOUPDATE_URL = "";

	/// <summary>In RELEASE the URL to the customizing file. Ignored if empty.</summary>
	public const string CUSTOMIZE_URL = "https://uevrdeluxe.z5.web.core.windows.net/CustomizingSettings.json";
}
#endif
