using System;

namespace UEVRDeluxe.Common;

public static class AzConstants {
	public const string AGENT_NAME = "UEVRDeluxe";

	public const string QUERYSTRING_NOCACHE = "nocache";

	public const string QUERYSTRING_INCLUDEENVIRONMENTS = "includeEnvironments";

	public static string GetProfileFileName(Guid profileID, string exeName) => $"{exeName}_{profileID:n}.zip";

	/// <summary>If the ZIP is larger than this in bytes it typically contains a log file ;-)</summary>
	public const long MAX_PROFILE_ZIP_SIZE = 20 * 1024 * 1024;
}

public static class UnrealConstants {
	/// <summary>These parts of a filename in an UEVR Exe are the environments</summary>
	public static readonly string[] FILENAME_ENVIRONMENTS = ["Win64", "WinGDK", "WinGRTS"];

	/// <summary>Usually the end of the filename name</summary>
	public const string FILENAME_POSTFIX_SHIPPING = "Shipping";
}

public static class UEVRBackendConstants {
	public const string UEVR_BACKEND_NAME_JOEYHODGE = "JoeyHodge";

	public const string UEVR_BACKEND_DLL = "UEVRBackend.dll";
	public const string UEVR_BACKEND_DLL_JOEYHODGE = "UEVRBackendJoeyHodge.dll";

	// Nightly release endpoints
	public const string UEVR_LATEST_NIGHTLY_URL = "https://github.com/praydog/UEVR-nightly/releases/latest";
	public const string UEVR_SEARCH_NIGHTLY_URL = "https://github.com/praydog/UEVR-nightly/releases?q=Nightly+{0}&expanded=true";

	// Local file that stores the URL of the downloaded nightly
	public const string UEVR_VERSION_PRAYDOG_FILENAME = "UEVRLink.txt";

	// JoeyHodge release endpoints
	public const string UEVR_LATEST_JOEYHODGE_URL = "https://github.com/joeyhodge/UEVR/releases/latest";
	public const string UEVR_DOWNLOAD_JOEYHODGE_URL = "https://github.com/joeyhodge/UEVR/releases/download/{0}/UEVRBackend.dll";
	public const string UEVR_VERSION_JOEYHODGE_FILENAME = "UEVRVersionJoeyHodge.txt";
}

public static class UEVRCmdArgs {
	// Command names used by the elevated helper EXE (UEVRDeluxeCmd)
	public const string UPDATEBACKEND = "UPDATEBACKEND";
	public const string UPDATEJOEYHODGEBACKEND = "UPDATEJOEYHODGEBACKEND";
	public const string INSTALLPROFILE = "INSTALLPROFILE";
	public const string UNINSTALLPROFILE = "UNINSTALLPROFILE";

}