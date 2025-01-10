#region Usings
using Gameloop.Vdf;
using Gameloop.Vdf.Linq;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

		// Check if cache is still valid
		if (cache != null && cache.AllInstallations.Count == allGames.Count
				&& cache.AllInstallations.All(c => allGames.Any(g => g.ID == c.ID && g.EXEName == c.EXEName && g.FolderPath == c.FolderPath))) {

			Debug.WriteLine("Taking cached game installations");
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
				|| d.Contains("Engine\\Binaries\\ThirdParty", StringComparison.OrdinalIgnoreCase))) 
				continue;

			// The find the EXE
			string[] exesPaths = Directory.GetFiles(game.FolderPath, "*.exe", SearchOption.AllDirectories);

			var exeProps = new List<ExecutableProp>();
			foreach (string exePath in exesPaths) {
				string exeFileName = Path.GetFileName(exePath);

				// Sometimes Crashreporters are sitting in prominent positions
				if (IGNORE_EXE_NAME_PARTS.Any(f => exeFileName.Contains(f, StringComparison.OrdinalIgnoreCase)))
					continue;

				var exe = new ExecutableProp { filePath = exePath };

				exe.isShipping = exeFileName.EndsWith("-Shipping.exe", StringComparison.OrdinalIgnoreCase);

				string[] pathParts = Path.GetDirectoryName(exePath).Split(Path.DirectorySeparatorChar);
				exe.directoryCount = pathParts.Length;

				string folder = pathParts.Last().ToLowerInvariant();
				exe.isInWinFolder = folder == "win32" || folder == "win64" || folder == "wingdk";
				exe.isInBinariesFolder = folder == "binaries";  // Not as good, but...

				exeProps.Add(exe);
			}

			var bestProps = exeProps.FirstOrDefault(g => g.isShipping && g.isInWinFolder)
				?? exeProps.FirstOrDefault(g => g.isShipping && g.isInBinariesFolder)
				?? exeProps.FirstOrDefault(g => g.isShipping)
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
			} else {
				Debug.WriteLine($"No executable found for {game.Name}");
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
			if (string.IsNullOrEmpty(appDataPath)) return allGames;


			var manifestsPath = Path.Combine(appDataPath, "Manifests");
			if (Directory.Exists(manifestsPath)) {
				var manifestPaths = Directory.GetFiles(manifestsPath, "*.item");

				foreach (var manifestPath in manifestPaths) {
					var manifest = JsonSerializer.Deserialize<EpicManifest>(File.ReadAllText(manifestPath), jsonOptions);
					if (!Directory.Exists(manifest.InstallLocation)  // Might be delete manually
						|| IGNORE_GAME_NAMES.Contains(manifest.DisplayName)) continue;  // e.g. "Unreal Engine"

					// IconURL later
					var game = new GameInstallation {
						StoreType = GameStoreType.Epic, EpicID = manifest.CatalogItemId, EpicNamespace = manifest.CatalogNamespace,
						FolderPath = manifest.InstallLocation, Name = manifest.DisplayName,
						ShellLaunchPath = $"com.epicgames.launcher://apps/{manifest.AppName}?action=launch&silent=true"
					};

					allGames.Add(game);
				}
			}

			// Try to resolve the Logos from Cache
			if (cacheGameInstallations != null) {
				foreach (var game in allGames)
					game.IconURL = cacheGameInstallations.FirstOrDefault(g => g.ID == game.ID)?.IconURL;
			}

			// If not, we must expensively read the EPIC Catalog cache
			if (allGames.Any(g => g.IconURL == null)) {
				Debug.WriteLine("Reading catalog cache");

				var catalogPath = Path.Combine(appDataPath, "Catalog", "catcache.bin");
				var catalog = new List<EpicCatalogItem>();
				if (File.Exists(catalogPath)) {
					var catalogCacheFile = File.ReadAllText(catalogPath);
					var json = Encoding.UTF8.GetString(Convert.FromBase64String(catalogCacheFile));
					catalog = JsonSerializer.Deserialize<List<EpicCatalogItem>>(json, jsonOptions);

					Debug.WriteLine($"Found {catalog.Count} cataloged EPIC items");

					foreach (var game in allGames) {
						var catalogItem = catalog.FirstOrDefault(c => c.Id == game.EpicID && c.Namespace == game.EpicNamespace);
						game.IconURL = catalogItem?.KeyImages?.OrderBy(k => k.Height)?.FirstOrDefault()?.Url ?? "DUMMY";
					}
				}
			}

			Debug.WriteLine($"Found {allGames.Count} installed EPIC games");
		} catch (Exception ex) {
			// Show must go on if an installation of one store is flawed
			Debug.WriteLine($"Failed to scan EPIC: {ex}");
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
			if (string.IsNullOrEmpty(steamInstallDir)) return allGames;  // not installed

			//if (!File.Exists(Path.Join(steamInstallDir, "steam.exe"))) return allGames;

			string vdfPath = Path.Join(steamInstallDir, "steamapps", "libraryfolders.vdf");
			if (!File.Exists(vdfPath)) throw new Exception("Steam Library Definition file not found");

			var steamLibraryDefinition = VdfConvert.Deserialize(File.ReadAllText(vdfPath));
			foreach (var steamLibDirDefinition in steamLibraryDefinition.Value.Children<VProperty>()) {
				var libDirData = steamLibDirDefinition.Value;
				var vtoken_libPath = libDirData.Value<string>("path");
				var vprop_apps = libDirData.Value<VObject>("apps");

				foreach (var gameDefinition in vprop_apps.Children<VProperty>()) {
					Debug.WriteLine($"Found {gameDefinition.Key}: {gameDefinition.Value}");

					if (long.TryParse(gameDefinition.Key, out long gameAppId)) {
						var gameManifestPath = Path.Join(vtoken_libPath, "steamapps", $"appmanifest_{gameAppId}.acf");

						if (File.Exists(gameManifestPath)) {
							var path_gameManifestDefinition = VdfConvert.Deserialize(File.ReadAllText(gameManifestPath));
							var gameManifestDefinition = path_gameManifestDefinition.Value;
							string relativeDirectoryName = gameManifestDefinition.Value<string>("installdir");

							var game = new GameInstallation { SteamID = gameAppId, StoreType = GameStoreType.Steam };
							game.Name = gameManifestDefinition.Value<string>("name");

							long lastPlayed = gameManifestDefinition.Value<long>("LastPlayed");
							if (lastPlayed > 0) game.LastPlayed = DateTimeOffset.FromUnixTimeSeconds(lastPlayed).DateTime;

							if (!IGNORE_GAME_NAMES.Contains(game.Name)) {
								game.FolderPath = Path.GetFullPath(Path.Join("steamapps", "common", relativeDirectoryName), vtoken_libPath);
								game.IconURL = $"https://cdn.cloudflare.steamstatic.com/steam/apps/{game.SteamID}/capsule_231x87.jpg"; //$"steam://install/{game.SteamID}";
								game.ShellLaunchPath = $"steam://rungameid/{game.SteamID}";

								// Sometimes guys manually delete the game folders
								if (Directory.Exists(game.FolderPath)) allGames.Add(game);
							}
						}
					}
				}
			}
		} catch (Exception ex) {
			// Show must go on if an installation of one store is flawed
			Debug.WriteLine($"Failed to scan EPIC: {ex}");
		}

		return allGames.OrderByDescending(g => g.LastPlayed ?? new DateTime()).ThenBy(g => g.Name).ToList();
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
	public string ID => SteamID.HasValue ? $"S{SteamID}" : $"E{EpicNamespace}|{EpicID}";

	public long? SteamID { get; set; }

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
	Epic
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
	public bool isInWinFolder, isInBinariesFolder, isShipping;
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