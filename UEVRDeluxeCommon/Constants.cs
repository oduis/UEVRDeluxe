using System;

namespace UEVRDeluxe.Common;

public static class AzConstants {
	public const string AGENT_NAME = "UEVRDeluxe";

	public const string QUERYSTRING_NOCACHE = "nocache";

	public const string QUERYSTRING_INCLUDEENVIRONMENTS = "includeEnvironments";

	public static string GetProfileFileName(Guid profileID, string exeName) => $"{exeName}_{profileID:n}.zip";

	/// <summary>If the ZIP is larger than this in bytes it typically contains a log file ;-)</summary>
	public const long MAX_PROFILE_ZIP_SIZE = 10 * 1024 * 1024;
}

public static class UnrealConstants {
	/// <summary>These parts of a filename in an UEVR Exe are the environments</summary>
	public static readonly string[] FILENAME_ENVIRONMENTS = ["Win64", "WinGDK", "WinGRTS"];

	/// <summary>Usually the end of the filename name</summary>
	public const string FILENAME_POSTFIX_SHIPPING = "Shipping";
}