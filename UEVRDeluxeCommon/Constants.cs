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
	// Praydogs Backend name is NULL to keep compatibility with older profiles
	public const string BACKEND_NAME_JOEYHODGE = "JoeyHodge";
	public const string BACKEND_NAME_PUREDARK_NIGHTLY = "PureDarkNightly";
	public const string BACKEND_NAME_PUREDARK_JOEYHODGE = "PureDarkJoeyHodge";
	public const string BACKEND_NAME_DORTAMUR = "Dortamur";

	public const string BACKEND_DLL_NAME = "UEVRBackend.dll";

	public const string BACKEND_FOLDER_PRAYDOG = "Praydog";
	public const string BACKEND_FOLDER_JOEYHODGE = "JoeyHodge";
	public const string BACKEND_FOLDER_PUREDARK_NIGHTLY = "PureDark_Nightly";
	public const string BACKEND_FOLDER_PUREDARK_JOEYHODGE = "PureDark_JoeyHodge";
	public const string BACKEND_FOLDER_DORTAMUR = "Dortamur";

	// Local file that stores the URL of the downloaded backend
	public const string VERSION_FILENAME = "UEVRVersion.txt";

	// Pradyog nightly endpoints
	public const string LATEST_PRAYDOG_NIGHTLY_URL = "https://github.com/praydog/UEVR-nightly/releases/latest";
	public const string SEARCH_PRAYDOG_NIGHTLY_URL = "https://github.com/praydog/UEVR-nightly/releases?q=Nightly+{0}&expanded=true";

	// JoeyHodge release endpoints
	public const string LATEST_JOEYHODGE_URL = "https://github.com/joeyhodge/UEVR/releases/latest";
	public const string DOWNLOAD_JOEYHODGE1_URL = "https://github.com/joeyhodge/UEVR/releases/download/{0}/UEVRBackend.dll";
	public const string DOWNLOAD_JOEYHODGE2_URL = "https://github.com/joeyhodge/UEVR/releases/download/{0}/DIBRUEVRBackend.dll";

	public const string LATEST_PUREDARK_URL = "https://github.com/PureDark/UEVR/releases/latest";
	public const string ASSETS_PUREDARK_URL = "https://github.com/PureDark/UEVR/releases/expanded_assets/{0}";

	public const string ASSETS_PUREDARK_LINK_REGEX = @"a href=""(?<URL>/PureDark/UEVR/releases.+?(nightly|joeyhodge).+?zip)""";

	public const string LATEST_DORTAMUR_URL = "https://github.com/dortamur/satisfactory-uevr-enhancements/releases/latest";
	public const string DOWNLOAD_DORTAMUR_URL = "https://github.com/dortamur/satisfactory-uevr-enhancements/releases/download/{0}/UEVR-Satisfactory-fix.zip";
}

/// <summary>Command names used by the elevated helper EXE (UEVRDeluxeCmd)</summary>
public static class UEVRCmdArgs {
	public const string UPDATE_PRAYDOG_BACKEND = "UPDATEPRAYDOGBACKEND";
	public const string UPDATE_JOEYHODGE_BACKEND = "UPDATEJOEYHODGEBACKEND";
	public const string UPDATE_PUREDARK_BACKENDS = "UPDATEPUREDARKBACKENDS";
	public const string UPDATE_DORTAMUR_BACKEND = "UPDATEDORTAMURBACKEND";
	public const string INSTALL_PROFILE = "INSTALLPROFILE";
	public const string UNINSTALL_PROFILE = "UNINSTALLPROFILE";
}