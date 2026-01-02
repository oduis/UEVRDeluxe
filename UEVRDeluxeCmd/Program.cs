#region Usings
using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using UEVRDeluxe.Common;
#endregion

namespace UEVRDeluxeCmd;

/// <summary>Helper Exe that is called as Admin by the UEVRDeluxe application</summary>
class Program {
	#region Main
	static async Task<int> Main(string[] args) {
		int resultCode = 0;
		string resultFilePathPath = null;

		try {
			if (args.Length < 2) throw new Exception("Too few arguments");
			resultFilePathPath = args[0];

			switch (args[1].ToUpperInvariant()) {
				case "UPDATEBACKEND":
					await UpdateBackendAsync(int.Parse(args[2]));
					break;

				case "INSTALLPROFILE":
					if (args.Length < 4) throw new Exception("INSTALLPROFILE requires profileRootFolder and gameExeFolder parameters");
					await InstallProfileAsync(args[2], args[3]);
					break;

				case "UNINSTALLPROFILE":
					if (args.Length < 4) throw new Exception("UNINSTALLPROFILE requires profileRootFolder and gameExeFolder parameters");
					await UninstallProfileAsync(args[2], args[3]);
					break;

				default:
					throw new Exception($"Unknown command: {args[1]}");
			}

			Console.WriteLine("Operation completed successfully");
		} catch (Exception ex) {
			Console.Error.WriteLine(ex.Message);

			try {
				if (resultFilePathPath != null) File.WriteAllText(resultFilePathPath, ex.Message);
			} catch { }

			resultCode = 0xff;
		}

		return resultCode;
	}
	#endregion

	#region UpdateBackend
	const string UEVR_SEARCH_NIGHTLY_URL = "https://github.com/praydog/UEVR-nightly/releases?q=Nightly+{0}&expanded=true";
	const string UEVR_VERSION_FILENAME = "UEVRLink.txt";

	/// <summary>Update UEVR backend from GitHub</summary>
	public static async Task UpdateBackendAsync(int nightlyNumber) {
		string zipUrl, sNightlyNumber, commitHash;

		string UEVRBaseDir = Path.Combine(AppContext.BaseDirectory, "..\\UEVR");
		string VersionFilePath = Path.Combine(UEVRBaseDir, UEVR_VERSION_FILENAME);

		byte[] zipData;
		using (var client = new HttpClient()) {
			sNightlyNumber = nightlyNumber.ToString("D5");
			Console.WriteLine($"Checking for UEVR nightly {sNightlyNumber}");

			string searchUrl = string.Format(UEVR_SEARCH_NIGHTLY_URL, sNightlyNumber);

			string html = await client.GetStringAsync(searchUrl);

			// e.g. <a href="/praydog/UEVR-nightly/releases/tag/nightly-01095-69fd6801eec8f9ede3c6667302b1740268b89c50" data-view-component="true" class="Link--primary Link" ...
			var match = Regex.Match(html, $"releases/tag/nightly-{sNightlyNumber}-([0-9a-f]+)");
			if (!match.Success) throw new Exception($"Could not find nightly version {sNightlyNumber} on GitHub");
			commitHash = match.Groups[1].Value;

			zipUrl = $"https://github.com/praydog/UEVR-nightly/releases/download/nightly-{sNightlyNumber}-{commitHash}/uevr.zip";

			if (File.Exists(VersionFilePath) && string.Equals(File.ReadAllText(VersionFilePath).Trim(), zipUrl)) {
				Console.WriteLine("UEVR backend is already up to date");
				return;
			}

			zipData = await client.GetByteArrayAsync(zipUrl);
		}

		using (var zipStream = new MemoryStream(zipData))
		using (var archive = new ZipArchive(zipStream)) {
			Directory.CreateDirectory(UEVRBaseDir);
			foreach (var entry in archive.Entries) {
				if (entry.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) {
					string destinationPath = Path.Combine(UEVRBaseDir, entry.Name);
					entry.ExtractToFile(destinationPath, true);

					File.SetLastAccessTimeUtc(destinationPath, entry.LastWriteTime.UtcDateTime);
				}
			}
		}

		File.WriteAllText(VersionFilePath, zipUrl);
	}
	#endregion

	#region InstallProfile
	/// <summary>Some games use paks in this subfolder as to load after the standard mods.</summary>
	const string MOD_SUBFOLDER = "~mods";


	/// <summary>Install a profile into the game folder by copying files defined in ProfileMeta.json</summary>
	public static async Task InstallProfileAsync(string profileRootFolder, string gameExeFolder) {
		if (string.IsNullOrWhiteSpace(profileRootFolder)) throw new ArgumentException("profileRootFolder is required");
		if (string.IsNullOrWhiteSpace(gameExeFolder)) throw new ArgumentException("gameExeFolder is required");

		var meta = LoadAndValidateProfileMeta(profileRootFolder);

		if (meta.FileCopies == null || meta.FileCopies.Count == 0) {
			Console.WriteLine("No files to copy for this profile");
			return;
		}

		foreach (var fc in meta.FileCopies) {
			string sourcePath = Path.Combine(profileRootFolder, fc.SourceFileRelProfile);
			if (!File.Exists(sourcePath)) throw new Exception($"Source file does not exist: {sourcePath}");

			string destFolder = GetResolvedPath(gameExeFolder, fc.DestinationFolderRelGameEXE);
			if (destFolder.EndsWith(@"\" + MOD_SUBFOLDER)) {
				// make sure the root mod folder exists
				string rootDestFolder = Directory.GetParent(destFolder)?.FullName;
				if (!Directory.Exists(rootDestFolder)) throw new Exception($"Root folder for {MOD_SUBFOLDER} does not exist: {rootDestFolder}");

				// Create ~mods folder, as it is usually not created by the game
				if (!Directory.Exists(destFolder)) Directory.CreateDirectory(destFolder);
			}

			if (!Directory.Exists(destFolder)) throw new Exception($"Game folder does not exist: {destFolder}");

			string destFilePath = Path.Combine(destFolder, Path.GetFileName(sourcePath));
			Console.WriteLine($"Copying '{sourcePath}' -> '{destFilePath}'");

			try {
				File.Copy(sourcePath, destFilePath, true);
			} catch (Exception ex) {
				throw new Exception($"Failed to copy to target '{destFilePath}': {ex.Message}", ex);
			}
		}
	}
	#endregion

	#region UninstallProfile
	/// <summary>Uninstall a profile by deleting files defined in ProfileMeta.json if they exist</summary>
	public static async Task UninstallProfileAsync(string profileRootFolder, string gameExeFolder) {
		if (string.IsNullOrWhiteSpace(profileRootFolder)) throw new ArgumentException("profileRootFolder is required");
		if (string.IsNullOrWhiteSpace(gameExeFolder)) throw new ArgumentException("gameExeFolder is required");

		var meta = LoadAndValidateProfileMeta(profileRootFolder);

		if (meta.FileCopies == null || meta.FileCopies.Count == 0) {
			Console.WriteLine("No files to remove for this profile");
			return;
		}

		foreach (var fc in meta.FileCopies) {
			string destFolder = GetResolvedPath(gameExeFolder, fc.DestinationFolderRelGameEXE);
			string destFilePath = Path.Combine(destFolder, Path.GetFileName(fc.SourceFileRelProfile));

			if (File.Exists(destFilePath)) {
				Console.WriteLine($"Deleting '{destFilePath}'");

				try {
					File.Delete(destFilePath);
				} catch (Exception ex) {
					throw new Exception($"Failed to delete target '{destFilePath}': {ex.Message}", ex);
				}
			} else {
				Console.WriteLine($"File not found, skipping: '{destFilePath}'");
			}
		}
	}
	#endregion

	#region Profile Helpers
	static ProfileMeta LoadAndValidateProfileMeta(string profileRootFolder) {
		string metaPath = Path.Combine(profileRootFolder, ProfileMeta.FILENAME);
		if (!File.Exists(metaPath)) throw new Exception($"Profile meta not found: {metaPath}");

		Console.WriteLine($"Reading profile metadata from {metaPath}");
		var meta = JsonSerializer.Deserialize(File.ReadAllText(metaPath), ProfileMetaJsonContext.Default.ProfileMeta);
		if (meta == null) throw new Exception("Failed to deserialize profile metadata");

		string check = meta.Check();
		if (check != null) throw new Exception($"Profile meta check failed: {check}");

		return meta;
	}

	const string LOCAL_APP_DATA = "%LOCALAPPDATA%";
	const string USER_PROFILE = "%USERPROFILE%";

	static string GetResolvedPath(string gameExeFolder, string relFolder) {
		if (string.IsNullOrWhiteSpace(relFolder)) return gameExeFolder;

		if (relFolder.StartsWith(LOCAL_APP_DATA, StringComparison.OrdinalIgnoreCase)) {
			string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
			relFolder = relFolder.Substring(LOCAL_APP_DATA.Length).TrimStart('\\', '/');
			return Path.Combine(localAppData, relFolder);
		} else if (relFolder.StartsWith(USER_PROFILE, StringComparison.OrdinalIgnoreCase)) {
			string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
			relFolder = relFolder.Substring(USER_PROFILE.Length).TrimStart('\\', '/');
			return Path.Combine(userProfile, relFolder);
		}

		return Path.Combine(gameExeFolder, relFolder);
	}
	#endregion
}
