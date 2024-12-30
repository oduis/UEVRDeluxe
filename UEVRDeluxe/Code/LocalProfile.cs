#region Usings
using IniParser;
using IniParser.Model;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using UEVRDeluxe.Common;
#endregion

namespace UEVRDeluxe.Code;

public class LocalProfile {
	const string CONFIG_FILENAME = "config.txt";

	/// <summary>Default settings if no file is found</summary>
	const string CONFIG_DEFAULT = """
		VR_RenderingMethod=0
		VR_SyncedSequentialMethod=1
		VR_UncapFramerate=true
		VR_Compatibility_SkipPostInitProperties=false
		VR_EnableDepth=false
		OpenXR_ResolutionScale=1.000000
		""";

	/// <summary>These config settings are overwritten with default when packing for submit</summary>
	const string CONFIG_OVERRIDE_SUBMIT = """
		FrameworkConfig_AdvancedMode=false
		FrameworkConfig_EnableL3R3Toggle=true
		FrameworkConfig_FontSize=16
		FrameworkConfig_ImGuiTheme=0
		FrameworkConfig_LogLevel=0
		OpenXR_IgnoreVirtualDesktopChecks=true
		OpenXR_ResolutionScale=1.000000
		VR_DPadShifting=true
		VR_DPadShiftingMethod=0
		VR_JoystickDeadzone=0.200000
		VR_PassDepthToRuntime=false
		VR_ShowFPSOverlay=false
		VR_ShowStatsOverlay=false
		LuaLoader_LogToDisk=false
		""";

	const string DUMMY_DESCRIPTION_MD = """
        ( See markdownguide.org for help with Markdown format )
        ## Installation and configuration
        ( add required mods or game settings here. Keep it short. Newlines in MD are double space a the end of a line )
        ## Restrictions
        ( What does not work or needs attention, like e.g. "Weapon xy has graphic failures." )
        ## Description
        ( Any more links, tips, etc. You can add more headers for more structure, e.g. changelogs )
        """;

	public string FolderPath { get; private set; }

	/// <summary>Content of the ProfileData.json</summary>
	public ProfileMeta Meta { get; private set; }

	/// <summary>Content of the Config.txt</summary>
	public IniData Config { get; private set; }

	/// <summary>Content of the ProfileDescription.md</summary>
	public string DescriptionMD { get; private set; }

	public static LocalProfile FromUnrealVRProfile(string exeName, bool createIfEmpty = false) {
		string directory = GetDirectoryName(exeName);

		if (Directory.Exists(directory)) return new LocalProfile(directory);
		if (createIfEmpty) return new LocalProfile(directory);
		return null;
	}

	static string GetDirectoryName(string exeName) {
		string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "UnrealVRMod");

		if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

		if (!string.IsNullOrWhiteSpace(exeName)) directory = Path.Combine(directory, exeName);
		return directory;
	}

	public static void ReplaceFromZip(string exeName, byte[] zipData) {
		if ((zipData?.Length ?? 0) == 0) throw new ArgumentNullException(nameof(zipData));

		string directoryName = GetDirectoryName(exeName);
		if (Directory.Exists(directoryName))
			Directory.Delete(directoryName, true);
		else
			Directory.CreateDirectory(directoryName);

		using var stream = new MemoryStream(zipData);
		using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
		archive.ExtractToDirectory(directoryName);
	}

	public LocalProfile(string folderPath) {
		this.FolderPath = folderPath;
		Load();
	}

	string ConfigFilePath => Path.Combine(FolderPath, CONFIG_FILENAME);
	string ProfileMetaPath => Path.Combine(FolderPath, ProfileMeta.FILENAME);
	string ProfileDescriptionPath => Path.Combine(FolderPath, ProfileMeta.DESCRIPTION_FILENAME);

	public void Load() {
		var parser = new FileIniDataParser();

		if (File.Exists(ConfigFilePath)) {
			try {
				Config = parser.ReadFile(ConfigFilePath, Encoding.UTF8);
			} catch (Exception ex) {
				throw new Exception($"Incorrect config{ConfigFilePath}: {ex.Message}");
			}
		} else {
			using (var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(CONFIG_DEFAULT)))
			using (var rdr = new StreamReader(memoryStream)) this.Config = parser.ReadData(rdr);
		}

		if (File.Exists(ProfileMetaPath)) {
			try {
				this.Meta = JsonSerializer.Deserialize<ProfileMeta>(File.ReadAllText(ProfileMetaPath));
			} catch (Exception ex) {
				throw new Exception($"Incorrect profile meta {ProfileMetaPath}: {ex.Message}");
			}
		} else {
			this.Meta = new();
		}

		if (File.Exists(ProfileDescriptionPath)) {
			this.DescriptionMD = File.ReadAllText(ProfileDescriptionPath);
		} else {
			this.DescriptionMD = string.Empty;
		}
	}

	public async Task SaveAsync() {
		if (Directory.Exists(FolderPath)) Directory.CreateDirectory(FolderPath);

		await WriteTextFileIfChangedAsync(ConfigFilePath, CleanedIni(Config));
		await WriteTextFileIfChangedAsync(ProfileMetaPath, JsonSerializer.Serialize(Meta, new JsonSerializerOptions { WriteIndented = true }));
		await WriteTextFileIfChangedAsync(ProfileDescriptionPath, DescriptionMD);
	}

	async Task WriteTextFileIfChangedAsync(string path, string content) {
		string oldContent = await File.ReadAllTextAsync(path);
		if (oldContent != content) {
			await File.WriteAllTextAsync(path, content);
			Debug.WriteLine($"{path} changed");
		}
	}

	async public Task<byte[]> PrepareForSubmitAsync(GameInstallation installation = null) {
		bool metasMissing = false;

		if (!File.Exists(Path.Combine(FolderPath, "config.txt")))
			throw new Exception("This is not a UEVR profile folder");

		// Delete temporary files
		string logFile = Path.Combine(FolderPath, "log.txt");
		if (File.Exists(logFile)) File.Delete(logFile);

		string crashdumpFile = Path.Combine(FolderPath, "crash.dmp");
		if (File.Exists(crashdumpFile)) File.Delete(crashdumpFile);

		if (string.IsNullOrWhiteSpace(Meta.EXEName)) {
			Meta.EXEName = installation?.EXEName ?? FolderPath.Substring(FolderPath.TrimEnd('\\').LastIndexOf(Path.DirectorySeparatorChar) + 1);
			Meta.ModifiedDate = DateTime.Today;

			// Some empty string so its easier to edit without NULLs
			Meta.GameName = installation?.Name ?? string.Empty;
			Meta.AuthorName = Environment.UserName ?? string.Empty;
			Meta.Remarks = string.Empty; Meta.GameVersion = string.Empty;
			Meta.MinEVRVersionDate = new DateTime(2024, 10, 31);   // Source code version release 1.05 october
			Meta.NullifyPlugins = true;

			File.WriteAllText(Path.Combine(FolderPath, ProfileMeta.FILENAME),
				JsonSerializer.Serialize(Meta, new JsonSerializerOptions { WriteIndented = true }));

			metasMissing = true;
		};

		if (!File.Exists(ProfileDescriptionPath)
			|| File.ReadAllText(ProfileDescriptionPath).StartsWith(DUMMY_DESCRIPTION_MD[..16])) {
			File.WriteAllText(ProfileDescriptionPath, DUMMY_DESCRIPTION_MD);

			metasMissing = true;
		}

		if (metasMissing) throw new Exception($"The profile lacks the metadata files {ProfileMeta.FILENAME}/{ProfileMeta.DESCRIPTION_FILENAME}. Skeletons have been created. Please fill them.");

		string err = Meta.Check();
		if (err != null) throw new Exception(err);

		// Pack up into a ZIP
		using var stream = new MemoryStream();
		using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true)) {
			// Create all directories, including empty ones
			foreach (var directoryPath in Directory.GetDirectories(FolderPath, "*", SearchOption.AllDirectories)) {
				var entry = archive.CreateEntry(Path.GetRelativePath(FolderPath, directoryPath) + "/", CompressionLevel.NoCompression);
			}

			var parser = new FileIniDataParser();
			foreach (var filePath in Directory.GetFiles(FolderPath, "*", SearchOption.AllDirectories)) {
				var entry = archive.CreateEntry(Path.GetRelativePath(FolderPath, filePath), CompressionLevel.SmallestSize);
				using var entryStream = entry.Open();

				var memIni = new MemoryStream();

				string filenameLower = Path.GetFileName(filePath).ToLowerInvariant();
				if (filenameLower == "config.txt") {
					// Replace some configs with default values for all
					IniData iniProfileData = parser.ReadFile(filePath);

					IniData iniOverrideData;
					using (var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(CONFIG_OVERRIDE_SUBMIT)))
					using (var rdr = new StreamReader(memoryStream)) iniOverrideData = parser.ReadData(rdr);

					iniProfileData.Merge(iniOverrideData);

					memIni = new(Encoding.UTF8.GetBytes(CleanedIni(iniProfileData)));
				} else if (filenameLower == "cvars_standard.txt") {
					IniData iniProfileData = parser.ReadFile(filePath);
					iniProfileData.Global.RemoveKey("Core_r.ScreenPercentage");

					memIni = new(Encoding.UTF8.GetBytes(CleanedIni(iniProfileData)));
				} else {
					byte[] content = File.ReadAllBytes(filePath);
					await memIni.WriteAsync(content, 0, content.Length);
				}

				memIni.Position = 0;

				memIni.CopyTo(entryStream);
				memIni.Close(); memIni.Dispose();
			}
		}

		byte[] data = stream.ToArray();

		if (data.Length > AzConstants.MAX_PROFILE_ZIP_SIZE) throw new Exception("Profile is too large. Does it contain files not necessary for UEVR?");

		//File.WriteAllBytes(@"C:\temp\pf.zip", data);

		return data;
	}

	static string CleanedIni(IniData data) => data.ToString().Replace(" = ", "=");
}
