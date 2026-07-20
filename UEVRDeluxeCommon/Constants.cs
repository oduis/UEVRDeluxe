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
	public const string BACKEND_NAME_JOEYHODGE = "JoeyHodge";
	public const string BACKEND_NAME_PUREDARK_NIGHTLY = "PureDarkNightly";
	public const string BACKEND_NAME_PUREDARK_JOEYHODGE = "PureDarkJoeyHodge";

	public const string BACKEND_DLL_PRAYDOG = "UEVRBackend.dll";
	public const string BACKEND_DLL_JOEYHODGE = "UEVRBackendJoeyHodge.dll";
	// Puredark has two, one based on praydog, one on Joedodge
	public const string BACKEND_DLL_PUREDARK_NIGHTLY = "UEVRBackendPureDarkNightly.dll";
	public const string BACKEND_DLL_PUREDARK_JOEYHODGE = "UEVRBackendPureDarkJoeyHodge.dll";

	// Local file that stores the URL of the downloaded nightly
	public const string VERSION_PRAYDOG_FILENAME = "UEVRLink.txt";
	public const string VERSION_JOEYHODGE_FILENAME = "UEVRVersionJoeyHodge.txt";

	// Points to the "nightly" version of the backend (PureDark has two)
	public const string VERSION_PUREDARK_FILENAME = "UEVRVersionPureDark.txt";

	// Pradyog nightly endpoints
	public const string LATEST_PRAYDOG_NIGHTLY_URL = "https://github.com/praydog/UEVR-nightly/releases/latest";
	public const string SEARCH_PRAYDOG_NIGHTLY_URL = "https://github.com/praydog/UEVR-nightly/releases?q=Nightly+{0}&expanded=true";

	// JoeyHodge release endpoints
	public const string LATEST_JOEYHODGE_URL = "https://github.com/joeyhodge/UEVR/releases/latest";
	public const string DOWNLOAD_JOEYHODGE_URL = "https://github.com/joeyhodge/UEVR/releases/download/{0}/UEVRBackend.dll";

	public const string LATEST_PUREDARK_URL = "https://github.com/PureDark/UEVR/releases/latest";
	public const string ASSETS_PUREDARK_URL = "https://github.com/PureDark/UEVR/releases/expanded_assets/{0}";

	public const string ASSETS_PUREDARK_LINK_REGEX = @"a href=""(?<URL>/PureDark/UEVR/releases.+?(nightly|joeyhodge).+?zip)""";
}

/// <summary>Command names used by the elevated helper EXE (UEVRDeluxeCmd)</summary>
public static class UEVRCmdArgs {
	public const string UPDATE_PRAYDOG_BACKEND = "UPDATEPRAYDOGBACKEND";
	public const string UPDATE_JOEYHODGE_BACKEND = "UPDATEJOEYHODGEBACKEND";
	public const string UPDATE_PUREDARK_BACKENDS = "UPDATEPUREDARKBACKENDS";
	public const string INSTALL_PROFILE = "INSTALLPROFILE";
	public const string UNINSTALL_PROFILE = "UNINSTALLPROFILE";
}