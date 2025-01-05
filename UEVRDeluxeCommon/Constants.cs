using System;

namespace UEVRDeluxe.Common;
public static class AzConstants {
	public const string AGENT_NAME = "UEVRDeluxe";

	public const string QUERYSTRING_NOCACHE = "nocache";

	public static string GetProfileFileName(Guid profileID, string exeName) => $"{exeName}_{profileID:n}.zip";

	/// <summary>If the ZIP is larger than this in bytes it typically contains a log file ;-)</summary>
	public const long MAX_PROFILE_ZIP_SIZE = 1024 * 1024;
}
