#region Usings
using System.ComponentModel.Design;
using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
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
				case UEVRCmdArgs.UPDATE_PRAYDOG_BACKEND:
					await UpdatePraydogBackendAsync(int.Parse(args[2]));
					break;

				case UEVRCmdArgs.UPDATE_JOEYHODGE_BACKEND:
					await UpdateJoeyHodgeBackendAsync(args[2]);
					break;

				case UEVRCmdArgs.UPDATE_PUREDARK_BACKENDS:
					await UpdatePureDarkBackendsAsync(args[2]);
					break;

				case UEVRCmdArgs.UPDATE_DORTAMUR_BACKEND:
					await UpdateDortamurBackendAsync(args[2]);
					break;

				case UEVRCmdArgs.INSTALL_PROFILE:
					if (args.Length < 4) throw new Exception("INSTALLPROFILE requires profileRootFolder and gameExeFolder parameters");
					await InstallProfileAsync(args[2], args[3]);
					break;

				case UEVRCmdArgs.UNINSTALL_PROFILE:
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

	#region UpdatePraydogBackend
	/// <summary>Update UEVR nightly backend from GitHub</summary>
	public static async Task UpdatePraydogBackendAsync(int nightlyNumber) {
		string zipUrl, sNightlyNumber, commitHash;

		string backendFolder = Path.Combine(UEVRBaseDir, UEVRBackendConstants.BACKEND_FOLDER_PRAYDOG);
		string versionFilePath = Path.Combine(backendFolder, UEVRBackendConstants.VERSION_FILENAME);

		using var client = new HttpClient();
		sNightlyNumber = nightlyNumber.ToString("D5");
		Console.WriteLine($"Checking for UEVR nightly {sNightlyNumber}");

		string searchUrl = string.Format(UEVRBackendConstants.SEARCH_PRAYDOG_NIGHTLY_URL, sNightlyNumber);

		string html = await client.GetStringAsync(searchUrl);

		// e.g. <a href="/praydog/UEVR-nightly/releases/tag/nightly-01095-69fd6801eec8f9ede3c6667302b1740268b89c50" data-view-component="true" class="Link--primary Link" ...
		var match = Regex.Match(html, $"releases/tag/nightly-{sNightlyNumber}-([0-9a-f]+)");
		if (!match.Success) throw new Exception($"Could not find nightly version {sNightlyNumber} on GitHub");
		commitHash = match.Groups[1].Value;

		zipUrl = $"https://github.com/praydog/UEVR-nightly/releases/download/nightly-{sNightlyNumber}-{commitHash}/uevr.zip";

		if (File.Exists(versionFilePath) && string.Equals(File.ReadAllText(versionFilePath).Trim(), zipUrl)) {
			Console.WriteLine("UEVR backend is already up to date");
			return;
		}

		await DownloadUpackZipDllsAsync(client, zipUrl, backendFolder);

		File.WriteAllText(versionFilePath, zipUrl);
	}
	#endregion

	#region UpdateJoeyHodgeBackendAsync
	/// <summary>Update UEVR backend from GitHub</summary>
	public static async Task UpdateJoeyHodgeBackendAsync(string name) {
		string prayDogBackendFolder = Path.Combine(UEVRBaseDir, UEVRBackendConstants.BACKEND_FOLDER_PRAYDOG);
		string backendFolder = Path.Combine(UEVRBaseDir, UEVRBackendConstants.BACKEND_FOLDER_JOEYHODGE);
		string versionFilePath = Path.Combine(backendFolder, UEVRBackendConstants.VERSION_FILENAME);

		Directory.CreateDirectory(backendFolder);

		// Copy all base DLLs from Praydog backend, as JoeyHodge ist just the plain dll
		foreach (string dllPath in Directory.GetFiles(prayDogBackendFolder, "*.dll")) {
			if (!dllPath.EndsWith(UEVRBackendConstants.BACKEND_DLL_NAME, StringComparison.OrdinalIgnoreCase)) {
				string destFile = Path.Combine(backendFolder, Path.GetFileName(dllPath));
				File.Copy(dllPath, destFile, true);
			}
		}

		using var client = new HttpClient();
		string fullUrl1 = string.Format(UEVRBackendConstants.DOWNLOAD_JOEYHODGE1_URL, HttpUtility.UrlEncode(name));
		string fullUrl2 = string.Format(UEVRBackendConstants.DOWNLOAD_JOEYHODGE2_URL, HttpUtility.UrlEncode(name));

		File.WriteAllBytes(Path.Combine(backendFolder, Path.GetFileName(new Uri(fullUrl1).AbsolutePath)),
			await client.GetByteArrayAsync(fullUrl1));
		File.WriteAllBytes(Path.Combine(backendFolder, Path.GetFileName(new Uri(fullUrl2).AbsolutePath)),
			await client.GetByteArrayAsync(fullUrl2));

		File.WriteAllText(versionFilePath, fullUrl1);  // only the first as version reference
	}
	#endregion

	#region UpdatePureDarkBackendsAsync
	/// <summary>Update UEVR backend from GitHub</summary>
	public static async Task UpdatePureDarkBackendsAsync(string name) {
		string nightlyFolder = Path.Combine(UEVRBaseDir, UEVRBackendConstants.BACKEND_FOLDER_PUREDARK_NIGHTLY);
		string joeyHodgeFolder = Path.Combine(UEVRBaseDir, UEVRBackendConstants.BACKEND_FOLDER_PUREDARK_JOEYHODGE);
		string nightlyVersionFilePath = Path.Combine(nightlyFolder, UEVRBackendConstants.VERSION_FILENAME);
		string joeyHodgeVersionFilePath = Path.Combine(joeyHodgeFolder, UEVRBackendConstants.VERSION_FILENAME);

		using var client = new HttpClient();
		// Puredark has two files, one based on praydog, one on joeyhodge.
		// Unfortunately the nameing changes, so regex search is important
		string assetsUtl = string.Format(UEVRBackendConstants.ASSETS_PUREDARK_URL, HttpUtility.UrlEncode(name));
		string assetsHtml = await client.GetStringAsync(assetsUtl);

		var matches = Regex.Matches(assetsHtml, UEVRBackendConstants.ASSETS_PUREDARK_LINK_REGEX);
		if (matches.Count != 2) throw new Exception($"Could not find any PureDark UEVR backend assets for {name}");

		string nightlyDownloadUrl = null;
		string joeyHodgeDownloadUrl = null;

		foreach (Match match in matches) {
			string zipUrl = "https://github.com" + match.Groups["URL"].Value;

			string backendFolder;
			bool isNightly = zipUrl.Contains("nightly", StringComparison.OrdinalIgnoreCase);
			if (isNightly) {
				nightlyDownloadUrl = zipUrl; backendFolder = nightlyFolder;
			} else {
				joeyHodgeDownloadUrl = zipUrl; backendFolder = joeyHodgeFolder;
			}

			await DownloadUpackZipDllsAsync(client, zipUrl, backendFolder);
		}

		// Transactional at the end.
		if (nightlyDownloadUrl != null) File.WriteAllText(nightlyVersionFilePath, nightlyDownloadUrl);
		if (joeyHodgeDownloadUrl != null) File.WriteAllText(joeyHodgeVersionFilePath, joeyHodgeDownloadUrl);
	}
	#endregion

	#region UpdateDortamurBackendAsync
	/// <summary>Update UEVR backend from GitHub</summary>
	public static async Task UpdateDortamurBackendAsync(string name) {
		string prayDogBackendFolder = Path.Combine(UEVRBaseDir, UEVRBackendConstants.BACKEND_FOLDER_PRAYDOG);
		string backendFolder = Path.Combine(UEVRBaseDir, UEVRBackendConstants.BACKEND_FOLDER_DORTAMUR);
		string versionFilePath = Path.Combine(backendFolder, UEVRBackendConstants.VERSION_FILENAME);

		Directory.CreateDirectory(backendFolder);

		// Copy all base DLLs from Praydog backend, as JoeyHodge ist just the plain dll
		foreach (string dllPath in Directory.GetFiles(prayDogBackendFolder, "*.dll")) {
			if (!dllPath.EndsWith(UEVRBackendConstants.BACKEND_DLL_NAME, StringComparison.OrdinalIgnoreCase)) {
				string destFile = Path.Combine(backendFolder, Path.GetFileName(dllPath));
				File.Copy(dllPath, destFile, true);
			}
		}

		using var client = new HttpClient();
		string fullUrl = string.Format(UEVRBackendConstants.DOWNLOAD_DORTAMUR_URL, HttpUtility.UrlEncode(name));

		await DownloadUpackZipDllsAsync(client, fullUrl, backendFolder);

		File.WriteAllText(versionFilePath, fullUrl);
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

	#region * Helpers
	static async Task DownloadUpackZipDllsAsync(HttpClient client, string zipUrl, string backendFolder) {
		Directory.CreateDirectory(backendFolder);

		byte[] zipData = await client.GetByteArrayAsync(zipUrl);
		using (var zipStream = new MemoryStream(zipData))
		using (var archive = new ZipArchive(zipStream)) {
			Directory.CreateDirectory(backendFolder);
			foreach (var entry in archive.Entries) {
				if (entry.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) {
					string destinationPath = Path.Combine(backendFolder, entry.Name);
					entry.ExtractToFile(destinationPath, true);

					File.SetLastAccessTimeUtc(destinationPath, entry.LastWriteTime.UtcDateTime);
				}
			}
		}
	}

	static string UEVRBaseDir => Path.Combine(AppContext.BaseDirectory, "..\\UEVR");

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
