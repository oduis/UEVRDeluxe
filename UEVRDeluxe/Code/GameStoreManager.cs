#region Usings
using Gameloop.Vdf;
using Gameloop.Vdf.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
#endregion

namespace UEVRDeluxe.Code;

public static class GameStoreManager {
	readonly static string[] IGNORE_GAME_NAMES = [
		"Steamworks Common Redistributables", "SteamVR", "PlayStation\u00AEVR2 App", "Godot Engine", "Unreal Engine", "Blender"];

	readonly static string[] IGNORE_EXE_NAME_PARTS = [
		"Setup.exe", "Setup_x64.exe", "Setup_x32.exe", "Launcher.exe", "CrashReport", "easyanticheat", "installer.exe", "crashpad_handler" ];

	readonly static string[] UNREAL_ENGINE_STRINGS = ["UnrealEngine", "UE4", "UE5", "UE6", "Epic Games"];

	#region FindAllUEVRGames
	static List<GameInstallation> gameInstallations;

	public async static Task<List<GameInstallation>> FindAllUEVRGamesAsync() {
		if (gameInstallations != null) return gameInstallations;  // If we e.g. get back from one game

		string rootFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "UEVRDeluxe");
		if (!Directory.Exists(rootFolder)) Directory.CreateDirectory(rootFolder);

		string gameInstallationCachePath = Path.Combine(rootFolder, "GameInstallationCache.json");

		GameInstallationCache cache = null;
		if (File.Exists(gameInstallationCachePath)) {
			cache = JsonSerializer.Deserialize<GameInstallationCache>(await File.ReadAllTextAsync(gameInstallationCachePath));
			if (cache.CacheStructureVersion != GameInstallationCache.LATEST_CACHE_STRUCTURE_VERSION) cache = null;
		}

		var allGames = new List<GameInstallation>();

		// This pretty quick, but catalogs in EPIC take a while. So try to augment with previous
		allGames.AddRange(FindAllSteamGames());
		allGames.AddRange(FindAllEPICGames(cache?.AllInstallations));
		allGames.AddRange(FindAllGOGGames());

		// Check if cache is still valid
		if (cache != null && cache.AllInstallations.Count == allGames.Count
				&& cache.AllInstallations.All(c => allGames.Any(g => g.ID == c.ID && g.FolderPath == c.FolderPath))) {

			Logger.Log.LogTrace($"Taking cached game installations returning {cache.FilteredInstallationIDs.Count} games");
			return cache.AllInstallations.Where(i => cache.FilteredInstallationIDs.Contains(i.ID)).ToList();
		}

		// Need to rebuild cache
		cache = new GameInstallationCache {
			CacheStructureVersion = GameInstallationCache.LATEST_CACHE_STRUCTURE_VERSION,
			// simple clone, since the EXE is modified below
			AllInstallations = allGames.ToArray().ToList()
		};

		// Find UE-Executable. This is more an art than a science and takes longer ;-)
		foreach (var game in allGames) {
			// First check if directories contain the magic directories that are specific to Unreal
			string[] alldirs = Directory.GetDirectories(game.FolderPath, "*", SearchOption.AllDirectories);

			if (!alldirs.Any(d => d.Contains("Engine\\Binaries\\Win64", StringComparison.OrdinalIgnoreCase)
				|| d.Contains("Engine\\Binaries\\ThirdParty", StringComparison.OrdinalIgnoreCase))) {
				Logger.Log.LogTrace($"No UE directories found for {game.Name}");
				continue;
			}

			// The find the EXE
			string[] exesPaths = Directory.GetFiles(game.FolderPath, "*.exe", SearchOption.AllDirectories);

			// the name of the game directory often partly occurs in the correct exe name
			// e.g folder (=game) \ReadyOrNot\, exe like ReadyOrNot.exe
			string folderShortName = Path.GetFileName(game.FolderPath.TrimEnd(Path.DirectorySeparatorChar)).Replace(" ", "");

			var exeProps = new List<ExecutableProp>();
			foreach (string exePath in exesPaths) {
				string exeFileName = Path.GetFileName(exePath);

				// Sometimes Crashreporters are sitting in prominent positions
				if (IGNORE_EXE_NAME_PARTS.Any(f => exeFileName.Contains(f, StringComparison.OrdinalIgnoreCase)))
					continue;

				var exe = new ExecutableProp { filePath = exePath };

				exe.isShipping = exeFileName.EndsWith("-Shipping.exe", StringComparison.OrdinalIgnoreCase);

				if (exeFileName.Length > 3 && folderShortName.Length > 3)
					exe.isSimilarName = folderShortName.Substring(0, 4).Equals(exeFileName.Substring(0, 4), StringComparison.OrdinalIgnoreCase);

				string[] pathParts = Path.GetDirectoryName(exePath).Split(Path.DirectorySeparatorChar);
				exe.directoryCount = pathParts.Length;

				string folder = pathParts.Last().ToLowerInvariant();
				exe.isInWinFolder = folder == "win32" || folder == "win64" || folder == "wingdk";
				exe.isInBinariesFolder = folder == "binaries";  // Not as good, but...

				exeProps.Add(exe);
			}

			var bestProps =
				exeProps.FirstOrDefault(g => g.isSimilarName && g.isShipping && g.isInWinFolder)
				?? exeProps.FirstOrDefault(g => g.isSimilarName && g.isShipping && g.isInBinariesFolder)
				?? exeProps.FirstOrDefault(g => g.isSimilarName && g.isShipping)
				?? exeProps.FirstOrDefault(g => g.isShipping && g.isInWinFolder)
				?? exeProps.FirstOrDefault(g => g.isShipping && g.isInBinariesFolder)
				?? exeProps.FirstOrDefault(g => g.isShipping)
				?? exeProps.FirstOrDefault(g => g.isSimilarName && g.isInWinFolder)
				?? exeProps.FirstOrDefault(g => g.isSimilarName && g.isInBinariesFolder)
				?? exeProps.FirstOrDefault(g => g.isInWinFolder)
				?? exeProps.FirstOrDefault(g => g.isInBinariesFolder)
				?? exeProps.OrderBy(g => g.directoryCount).FirstOrDefault();

			if (bestProps != null) {
				// in Steam there are sometimes exes next to the shipping exe, like in Star Wars fallen order.
				// So we need to check if the exe is in the same folder as the shipping exe
				if (bestProps.isShipping) {
					string folder = Path.GetDirectoryName(bestProps.filePath);
					if (exeProps.Any(e => e.filePath != bestProps.filePath && Path.GetDirectoryName(e.filePath) == folder)) {
						bestProps = exeProps.First(e => e.filePath != bestProps.filePath && Path.GetDirectoryName(e.filePath) == folder);
					}
				}

				game.EXEName = Path.GetFileNameWithoutExtension(bestProps.filePath);

				Logger.Log.LogTrace($"{game.Name} executable: {bestProps.filePath}");
			} else {
				Logger.Log.LogTrace($"No executable found for {game.Name}");
			}
		}

		cache.FilteredInstallationIDs = allGames.Where(g => g.EXEName != null).Select(g => g.ID).Order().ToList();

		allGames.RemoveAll(g => g.EXEName == null);

		await File.WriteAllTextAsync(gameInstallationCachePath, JsonSerializer.Serialize(cache));

		gameInstallations = allGames;

		return allGames;
	}
	#endregion

	#region FindAllEPICGames
	static List<GameInstallation> FindAllEPICGames(List<GameInstallation> cacheGameInstallations) {
		var allGames = new List<GameInstallation>();

		try {
			var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

			string appDataPath = ReadWin32RegistryValue("SOFTWARE\\Epic Games\\EpicGamesLauncher", "AppDataPath");
			if (string.IsNullOrEmpty(appDataPath)) {
				Logger.Log.LogTrace("EPIC not installed");
				return allGames;
			}

			Logger.Log.LogTrace($"EPIC installed in {appDataPath}");

			var manifestsPath = Path.Combine(appDataPath, "Manifests");
			if (Directory.Exists(manifestsPath)) {
				var manifestPaths = Directory.GetFiles(manifestsPath, "*.item");

				foreach (var manifestPath in manifestPaths) {
					try {
						var manifest = JsonSerializer.Deserialize<EpicManifest>(File.ReadAllText(manifestPath), jsonOptions);
						if (!Directory.Exists(manifest.InstallLocation)  // Might be delete manually
							|| IGNORE_GAME_NAMES.Contains(manifest.DisplayName)) continue;  // e.g. "Unreal Engine"

						Logger.Log.LogTrace($"Game {manifest.DisplayName}");

						// IconURL later
						var game = new GameInstallation {
							StoreType = GameStoreType.Epic, EpicID = manifest.CatalogItemId, EpicNamespace = manifest.CatalogNamespace,
							FolderPath = manifest.InstallLocation, Name = manifest.DisplayName,
							ShellLaunchPath = $"com.epicgames.launcher://apps/{manifest.AppName}?action=launch&silent=true"
						};

						allGames.Add(game);
					} catch (Exception ex) {
						// For the guys still running their corrupted hard drives... show must go on
						Logger.Log.LogError($"Failed to read EPIC manifest {manifestPath}: {ex.Message}");
					}
				}

				// Try to resolve the Logos from Cache
				if (cacheGameInstallations != null) {
					foreach (var game in allGames)
						game.IconURL = cacheGameInstallations.FirstOrDefault(g => g.ID == game.ID)?.IconURL;
				}

				// If not, we must expensively read the EPIC Catalog cache
				if (allGames.Any(g => g.IconURL == null)) {
					Logger.Log.LogTrace("Reading catalog cache");

					var catalogPath = Path.Combine(appDataPath, "Catalog", "catcache.bin");
					var catalog = new List<EpicCatalogItem>();
					if (File.Exists(catalogPath)) {
						var catalogCacheFile = File.ReadAllText(catalogPath);
						var json = Encoding.UTF8.GetString(Convert.FromBase64String(catalogCacheFile));
						catalog = JsonSerializer.Deserialize<List<EpicCatalogItem>>(json, jsonOptions);

						Logger.Log.LogTrace($"Found {catalog.Count} cataloged EPIC items");

						foreach (var game in allGames) {
							var catalogItem = catalog.FirstOrDefault(c => c.Id == game.EpicID && c.Namespace == game.EpicNamespace);
							game.IconURL = catalogItem?.KeyImages?.OrderBy(k => k.Height)?.FirstOrDefault()?.Url
								?? "/Assets/GenericGameLogo.jpg";
						}
					}
				}

				Logger.Log.LogTrace($"Found {allGames.Count} installed EPIC games");
			}
		} catch (Exception ex) {
			// Show must go on if an installation of one store is flawed
			Logger.Log.LogCritical(ex, "Failed to scan EPIC");
		}

		return allGames.OrderByDescending(g => g.Name).ToList();
	}
	#endregion

	#region FindAllSteamGames
	static List<GameInstallation> FindAllSteamGames() {
		var allGames = new List<GameInstallation>();

		try {
			// Find Steam Root dir
			string steamInstallDir = ReadWin32RegistryValue(@"SOFTWARE\Valve\Steam", "InstallPath");
			if (string.IsNullOrEmpty(steamInstallDir)) {
				Logger.Log.LogTrace("STEAM not installed");
				return allGames;
			}

			string vdfPath = Path.Join(steamInstallDir, "steamapps", "libraryfolders.vdf");
			if (!File.Exists(vdfPath)) throw new Exception("Steam Library Definition file not found");

			Logger.Log.LogTrace($"Reading Steam Library Definition {vdfPath}");

			var steamLibraryDefinition = VdfConvert.Deserialize(File.ReadAllText(vdfPath));
			foreach (var steamLibDirDefinition in steamLibraryDefinition.Value.Children<VProperty>()) {
				var libDirData = steamLibDirDefinition.Value;
				var vtoken_libPath = libDirData.Value<string>("path");
				var vprop_apps = libDirData.Value<VObject>("apps");

				foreach (var gameDefinition in vprop_apps.Children<VProperty>()) {
					Logger.Log.LogTrace($"Found game {gameDefinition.Key}: {gameDefinition.Value}");

					if (long.TryParse(gameDefinition.Key, out long gameAppId)) {
						var gameManifestPath = Path.Join(vtoken_libPath, "steamapps", $"appmanifest_{gameAppId}.acf");

						if (File.Exists(gameManifestPath)) {
							try {
								var path_gameManifestDefinition = VdfConvert.Deserialize(File.ReadAllText(gameManifestPath));
								var gameManifestDefinition = path_gameManifestDefinition.Value;
								string relativeDirectoryName = gameManifestDefinition.Value<string>("installdir");

								var game = new GameInstallation { SteamID = gameAppId, StoreType = GameStoreType.Steam };
								game.Name = gameManifestDefinition.Value<string>("name");

								Logger.Log.LogTrace($"Game {game.Name}");

								long lastPlayed = gameManifestDefinition.Value<long>("LastPlayed");
								if (lastPlayed > 0) game.LastPlayed = DateTimeOffset.FromUnixTimeSeconds(lastPlayed).DateTime;

								if (!IGNORE_GAME_NAMES.Contains(game.Name)) {
									game.FolderPath = Path.GetFullPath(Path.Join("steamapps", "common", relativeDirectoryName), vtoken_libPath);
									game.IconURL = $"https://cdn.cloudflare.steamstatic.com/steam/apps/{game.SteamID}/capsule_231x87.jpg"; //$"steam://install/{game.SteamID}";
									game.ShellLaunchPath = $"steam://rungameid/{game.SteamID}";

									// Sometimes guys manually delete the game folders
									if (Directory.Exists(game.FolderPath))
										allGames.Add(game);
									else
										Logger.Log.LogWarning($"Steam game not installed any more in {game.FolderPath}");
								}
							} catch (Exception ex) {
								// For the guys still running their corrupted hard drives... show must go on
								Logger.Log.LogCritical(ex, "Failed to read Steam manifest {0}", gameManifestPath);
							}
						} else Logger.Log.LogWarning($"Steam game manifest not found for {gameManifestPath}");
					} else Logger.Log.LogWarning($"Steam game ID not a number {gameDefinition.Key}");
				}
			}
		} catch (Exception ex) {
			// Show must go on if an installation of one store is flawed
			Logger.Log.LogCritical(ex, "Failed to scan Steam");
		}

		return allGames.OrderByDescending(g => g.LastPlayed ?? new DateTime()).ThenBy(g => g.Name).ToList();
	}
	#endregion

	#region FindAllGOGGames
	static List<GameInstallation> FindAllGOGGames() {
		var allGames = new List<GameInstallation>();

		try {
			// Find GOG launcher
			string gogExeName = ReadWin32RegistryValue(@"SOFTWARE\GOG.com\GalaxyClient", "clientExecutable");
			if (string.IsNullOrEmpty(gogExeName)) {
				Logger.Log.LogTrace("GOG not installed");
				return allGames;
			}

			Logger.Log.LogTrace($"Found GOG Client {gogExeName}");

			var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);

			var keyGames = hklm.OpenSubKey(@"SOFTWARE\GOG.com\Games", false);

			foreach (string subkeyName in keyGames.GetSubKeyNames()) {
				var gameKey = keyGames.OpenSubKey(subkeyName, false);

				string gameName = gameKey.GetValue("gameName") as string;
				if (string.IsNullOrEmpty(gameName)) continue;

				Logger.Log.LogTrace($"Found GOG game {gameName}");
				var game = new GameInstallation {
					GOGID = long.Parse(subkeyName),
					StoreType = GameStoreType.GOG,
					Name = gameName,
#if DEBUG
					//EXEName = "Test",  // Force visibility if you don't want to buy a UE game on GOG just to test
#endif
					FolderPath = gameKey.GetValue("path") as string,
					IconURL = "/Assets/GOGLogo.jpg",
					ShellLaunchPath = ($"{gameKey.GetValue("launchCommand") as string} {gameKey.GetValue("launchParam") as string}").Trim()
				};

				if (Directory.Exists(game.FolderPath)) allGames.Add(game);
			}
		} catch (Exception ex) {
			// Show must go on if an installation of one store is flawed
			Logger.Log.LogCritical(ex, "Failed to scan GOG");
		}

		return allGames.OrderBy(g => g.Name).ToList();
	}
	#endregion

	#region * Helpers
	static string ReadWin32RegistryValue(string keyPath, string valueName) {
		using (var regRoot = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
		using (var regKey = regRoot.OpenSubKey(keyPath, false)) {
			if (regKey == null) return null;  // not installed

			var valueKind = regKey.GetValueKind(valueName);
			if (valueKind == RegistryValueKind.String || valueKind == RegistryValueKind.ExpandString)
				return regKey.GetValue(valueName) as string;

			return null;
		}
	}
	#endregion
}

public class GameInstallation {
	/// <summary>Global ID for comparisons</summary>
	[JsonIgnore]
	public string ID => StoreType switch {
		GameStoreType.Steam => $"S{SteamID}",
		GameStoreType.Epic => $"E{EpicNamespace}|{EpicID}",
		GameStoreType.GOG => $"G{GOGID}",
		_ => throw new NotImplementedException()
	};

	public long? SteamID { get; set; }

	public long? GOGID { get; set; }

	public string EpicID { get; set; }
	public string EpicNamespace { get; set; }

	public string Name { get; set; }

	public string FolderPath { get; set; }

	public string IconURL { get; set; }

	public GameStoreType StoreType { get; set; }

	/// <summary>The real executable (not the launchers above) without .exe</summary>
	public string EXEName { get; set; }

	public string ShellLaunchPath { get; set; }

	public DateTime? LastPlayed { get; set; }
}

public enum GameStoreType {
	Steam,
	Epic,
	GOG
}

/// <summary>Disk representation.</summary>
internal class GameInstallationCache {
	public const int LATEST_CACHE_STRUCTURE_VERSION = 1;

	/// <summary>What version was it built with?</summary>
	/// <remarks>For future expansion, to determine if we'd need to recreate the cache because of structural changes.</remarks>
	public int CacheStructureVersion { get; set; }

	/// <summary>Before the expensive filter</summary>
	public List<GameInstallation> AllInstallations { get; set; }

	/// <summary>Just the resulting, filtered ones</summary>
	public List<string> FilteredInstallationIDs { get; set; }
}

internal class ExecutableProp {
	public string filePath;
	public bool isInWinFolder, isInBinariesFolder, isShipping, isSimilarName;
	public int directoryCount;
}

#region * Epic Manifests and Cache data
/// <summary>JSON representation of an EPIC Store manifest file</summary>
class EpicManifest {
	public string DisplayName { get; set; }

	public string LaunchExecutable { get; set; }

	public string InstallLocation { get; set; }

	public string CatalogNamespace { get; set; }

	public string CatalogItemId { get; set; }

	public string AppName { get; set; }
}


class EpicCatalogCategory {
	public string Path { get; set; }
}

class EpicCatalogReleaseInfo {
	public string AppId { get; set; }
	public List<string> Platform { get; set; }
	public string DateAdded { get; set; }
}

class EpicCatalogImage {
	public int Height { get; set; }
	public string Url { get; set; }
}

class EpicCatalogItem {
	public string Id { get; set; }
	public string Namespace { get; set; }
	public string Title { get; set; }
	public List<EpicCatalogCategory> Categories { get; set; }
	public List<EpicCatalogReleaseInfo> ReleaseInfo { get; set; }
	public List<EpicCatalogImage> KeyImages { get; set; }
}
#endregion