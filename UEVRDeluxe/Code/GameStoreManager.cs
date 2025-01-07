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
#endregion

namespace UEVRDeluxe.Code;

public static class GameStoreManager {
	readonly static string[] IGNORE_GAME_NAMES = ["Steamworks Common Redistributables", "SteamVR"];

	readonly static string[] UNREAL_ENGINE_STRINGS = ["UnrealEngine", "UE4", "UE5", "UE6", "Epic Games"];

	#region FindAllUEVRGames
	public static List<GameInstallation> FindAllUEVRGames() {
		var allGames = new List<GameInstallation>();

		allGames.AddRange(FindAllSteamGames());
		allGames.AddRange(FindAllEPICGames());

		// Find UE-Executable. This is more an art than a science ;-)
		foreach (var game in allGames) {
			string[] exesPaths = [];
			try
			{
				exesPaths = Directory.GetFiles(game.FolderPath, "*.exe", SearchOption.AllDirectories);
			} catch (DirectoryNotFoundException)
			{
				//if the directory doesn't exist we will get an error, if so ignore the error
			}
			
			var exeProps = new List<ExecutableProp>();
			foreach (string exePath in exesPaths) {
				var exe = new ExecutableProp { filePath = exePath };

				exe.isShipping = Path.GetFileName(exePath).EndsWith("-Shipping.exe", System.StringComparison.OrdinalIgnoreCase);

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

				if (!exeProps.Any(e => e.isShipping)) {
					// This is how UEVRFrontend does it
					// Check if going up the parent directories reveals the directory "\Engine\Binaries\ThirdParty".
					var parentPath = Path.GetDirectoryName(bestProps.filePath);
					for (int i = 0; i < 10; ++i) {  // Limit the number of directories to move up to prevent endless loops.
						if (parentPath == null) game.EXEName = null;

						if (Directory.Exists(parentPath + "\\Engine\\Binaries\\ThirdParty") ||
							Directory.Exists(parentPath + "\\Engine\\Binaries\\Win64")) break;

						parentPath = Path.GetDirectoryName(parentPath);
					}

					/* Pretty resource intensive
					string fileContent = File.ReadAllText(bestProps.filePath, Encoding.ASCII);

					// Check for common Unreal Engine strings
					if (!UNREAL_ENGINE_STRINGS.Any(s => fileContent.Contains(s, StringComparison.OrdinalIgnoreCase)))
						game.EXEName = null;
					Debug.WriteLine($"UE strings in {game.Name}: {game.EXEName != null}");
					*/
				}
			} else {
				Debug.WriteLine($"No executable found for {game.Name}");
			}
		}

		allGames.RemoveAll(g => g.EXEName == null);

		return allGames;
	}
	#endregion

	#region FindAllEPICGames
	static List<GameInstallation> FindAllEPICGames() {
		var allGames = new List<GameInstallation>();

		try {
			var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

			string appDataPath = ReadWin32RegistryValue("SOFTWARE\\Epic Games\\EpicGamesLauncher", "AppDataPath");
			if (string.IsNullOrEmpty(appDataPath)) return allGames;

			// We need the catalog for Logo resultion
			var catalogPath = Path.Combine(appDataPath, "Catalog", "catcache.bin");
			var catalog = new List<EpicCatalogItem>();
			if (File.Exists(catalogPath)) {
				var catalogCacheFile = File.ReadAllText(catalogPath);
				var json = Encoding.UTF8.GetString(Convert.FromBase64String(catalogCacheFile));
				catalog = JsonSerializer.Deserialize<List<EpicCatalogItem>>(json, jsonOptions);
			}
			Debug.WriteLine($"Found {catalog.Count} cataloged EPIC items");

			var manifestsPath = Path.Combine(appDataPath, "Manifests");
			if (Directory.Exists(manifestsPath)) {
				var manifestPaths = Directory.GetFiles(manifestsPath, "*.item");

				foreach (var manifestPath in manifestPaths) {
					var manifest = JsonSerializer.Deserialize<EpicManifest>(File.ReadAllText(manifestPath), jsonOptions);

					var game = new GameInstallation {
						StoreType = GameStoreType.Epic, EpicId = manifest.CatalogItemId, EpicNamespace = manifest.CatalogNamespace,
						FolderPath = manifest.InstallLocation, Name = manifest.DisplayName,
						ShellLaunchPath = $"com.epicgames.launcher://apps/{manifest.AppName}?action=launch&silent=true"
					};

					var catalogItem = catalog.FirstOrDefault(c => c.Id == game.EpicId && c.Namespace == game.EpicNamespace);
					game.IconURL = catalogItem?.KeyImages?.OrderBy(k => k.Height)?.FirstOrDefault()?.Url ?? "DUMMY";

					allGames.Add(game);
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
	public long SteamID { get; set; }

	public string EpicId { get; set; }
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