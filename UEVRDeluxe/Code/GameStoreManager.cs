#region Usings
using Gameloop.Vdf;
using Gameloop.Vdf.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using PeNet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Xml.Linq;
using Windows.Management.Deployment;
#endregion

namespace UEVRDeluxe.Code;

public static class GameStoreManager {
	readonly static string[] IGNORE_GAME_NAMES = [
		"Steamworks Common Redistributables", "SteamVR", "PlayStation\u00AEVR2 App", "Godot Engine", "Unreal Engine", "Blender",
		"Ubisoft Connect", "REDlauncher", "Epic Games Launcher", "Epic Online Services"];

	/// <summary>Publishers allowed to be considered as games from native Windows uninstall entries, checked with containing</summary>
	/// <remarks>We want to avoid scanning directories of non games</remarks>
	readonly static string[] ALLOWED_PUBLISHERS_CONTAINS = [ "2K", "BANDAI NAMCO Entertainment",
		"CD PROJEKT RED", "Deep Silver", "Electronic Arts", "Epic Games", "Focus Entertainment",
		"Focus Home", "Gameloft", "Gearbox Publishing", "KRAFTON", "NEXON Korea",
		"People Can Fly", "SQUARE ENIX", "Sony Interactive", "THQ Nordic",
		"Ubisoft", "Warner Bros", "WB Games" ];

	#region FindAllUEVRGames
	static List<GameInstallation> gameInstallations;

	public async static Task<List<GameInstallation>> FindAllUEVRGamesAsync(bool forceRescan) {
		if (gameInstallations != null && !forceRescan) return gameInstallations;  // If we e.g. get back from one game

		if (forceRescan) Logger.Log.LogInformation("Forced game scan");

		string rootFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "UEVRDeluxe");
		if (!Directory.Exists(rootFolder)) Directory.CreateDirectory(rootFolder);

		string gameInstallationCachePath = Path.Combine(rootFolder, "GameInstallationCache.json");

		GameInstallationCache cache = null;
		if (!forceRescan && File.Exists(gameInstallationCachePath)) {
			cache = JsonSerializer.Deserialize<GameInstallationCache>(await File.ReadAllTextAsync(gameInstallationCachePath));
			if (cache.CacheStructureVersion != GameInstallationCache.LATEST_CACHE_STRUCTURE_VERSION) cache = null;
		}

		var allGames = new List<GameInstallation>();

		// This pretty quick, but catalogs in EPIC take a while. So try to augment with previous
		allGames.AddRange(FindAllXBoxGames());
		allGames.AddRange(FindAllSteamGames());
		allGames.AddRange(FindAllEPICGames(cache?.AllInstallations));
		allGames.AddRange(FindAllGOGGames());
		allGames.AddRange(FindAllEAGames());
		allGames.AddRange(FindAllWindowsUninstalls());

		// Remove all games already VR by name. They are often on Unreal engine...
		allGames.RemoveAll(g => g.Name.EndsWith(" VR", StringComparison.OrdinalIgnoreCase) || g.Name.Contains(" VR ", StringComparison.OrdinalIgnoreCase));

		// Remove Windows uninstall entries if another store already contains that folder (give priority to regular stores)
		try {
			var nonWindows = allGames.Where(g => g.StoreType != GameStoreType.Windows && !string.IsNullOrEmpty(g.FolderPath)).ToList();
			var windows = allGames.Where(g => g.StoreType == GameStoreType.Windows && !string.IsNullOrEmpty(g.FolderPath)).ToList();

			foreach (var win in windows) {
				string winPath = Path.GetFullPath(win.FolderPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
				if (nonWindows.Any(n => {
					string nPath = Path.GetFullPath(n.FolderPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
					return winPath.StartsWith(nPath, StringComparison.OrdinalIgnoreCase);
				})) {
					Logger.Log.LogTrace($"Removing Windows uninstall entry {win.Name} because another store already contains it");
					allGames.Remove(win);
				}
			}
		} catch (Exception ex) {
			Logger.Log.LogWarning(ex, "Failed to filter Windows uninstall entries");
		}

		// Check if cache is still valid
		if (!forceRescan && cache != null && cache.AllInstallations.Count == allGames.Count
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

		// Read the settings from web
		CustomizingSettings settings = null;
		if (!string.IsNullOrEmpty(CompiledSecret.CUSTOMIZE_URL)) {
			try {
				Logger.Log.LogTrace("Reading customizing settings");

				using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) }) {
					settings = await client.GetFromJsonAsync<CustomizingSettings>(CompiledSecret.CUSTOMIZE_URL);
				}
			} catch (Exception ex) {
				Logger.Log.LogError(ex, "Failed to read customizing settings");
				settings = null;
			}
		}

		if (settings == null) settings = new() { EXENamePartsToIgnore = [], EXENameLauncher = null };

		Logger.Log.LogTrace($"Found {allGames.Count} games");

		// Some guys set symlinks, which lead to endless loops if set incorrectly
		var enumOptionsSubdirectories = new EnumerationOptions {
			RecurseSubdirectories = true,
			AttributesToSkip = FileAttributes.ReparsePoint,
			IgnoreInaccessible = true
		};

		// Find UE-Executable. This is more an art than a science and takes longer ;-)
		foreach (var game in allGames) {
			Logger.Log.LogTrace($"Scanning {game.Name} in {game.FolderPath}");
#if DEBUG
			// Force visibility if you don't want to buy a UE game just to test
			//if (game.EAContentIDs != null) { game.EXEName = "Test"; continue; }
#endif
			try {
				// First check if directories contain the magic directories that are specific to Unreal
				string[] alldirs = Directory.GetDirectories(game.FolderPath, "*", enumOptionsSubdirectories);

				if (!alldirs.Any(d => d.Contains("Engine\\Binaries\\Win64", StringComparison.OrdinalIgnoreCase)
					|| d.Contains("Engine\\Binaries\\ThirdParty", StringComparison.OrdinalIgnoreCase))) {
					Logger.Log.LogTrace($"No UE directories found for {game.Name}");
					continue;
				}

				// The find the EXE
				string[] exesPaths = Directory.GetFiles(game.FolderPath, "*.exe", enumOptionsSubdirectories);

				// the name of the game directory often partly occurs in the correct exe name
				// e.g folder (=game) \ReadyOrNot\, exe like ReadyOrNot.exe
				string folderShortName = Path.GetFileName(game.FolderPath.TrimEnd(Path.DirectorySeparatorChar)).Replace(" ", "");

				var exeProps = new List<ExecutableProp>();
				foreach (string exePath in exesPaths) {
					string exeFileName = Path.GetFileName(exePath);

					// Sometimes Crashreporters are sitting in prominent positions
					if (settings.EXENamePartsToIgnore.Any(f => exeFileName.Contains(f, StringComparison.OrdinalIgnoreCase)))
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

					// Check for typical UE DLLs. But this does not work on e.g. XBox, since they are security boxed
					// Since this is performance intensive, only if its not clear from the file name
					if (!exe.isShipping && !exe.filePath.Contains(@"\Program Files\WindowsApps\", StringComparison.OrdinalIgnoreCase)) {
						try {
							var peFile = new PeFile(exe.filePath);
							// Not UE engine links, but all UE exes seem to reference these ones
							exe.isPESignatureOK = peFile.ImportedFunctions.Count(f => f.DLL.StartsWith("api-ms-win")) >= 3;
						} catch (Exception ex) {
							Logger.Log.LogWarning(ex, $"Failed to PE-check file {exe.filePath}");
							exe.isPESignatureOK = false;
						}
					}

					exeProps.Add(exe);
				}

				// Heuristic to find the best executable. DLLImport is the best checker,
				// however sometimes there are EXE als launchers next to them, so we may not throw away all non-DLLImport exes.
				// And sometimes PE-Check fails for security reasons, so we need to have a fallback.
				ExecutableProp bestProps = exeProps.FirstOrDefault(g => g.isSimilarName && g.isShipping && g.isInWinFolder)
						?? exeProps.FirstOrDefault(g => g.isSimilarName && g.isShipping && g.isInBinariesFolder)
						?? exeProps.FirstOrDefault(g => g.isSimilarName && g.isShipping)
						?? exeProps.FirstOrDefault(g => g.isShipping && g.isInWinFolder)
						?? exeProps.FirstOrDefault(g => g.isShipping && g.isInBinariesFolder)
						?? exeProps.FirstOrDefault(g => g.isShipping)
						?? exeProps.FirstOrDefault(g => g.isPESignatureOK && g.isSimilarName && g.isInWinFolder)
						?? exeProps.FirstOrDefault(g => g.isPESignatureOK && g.isSimilarName && g.isInBinariesFolder)
						?? exeProps.FirstOrDefault(g => g.isPESignatureOK && g.isInWinFolder)
						?? exeProps.FirstOrDefault(g => g.isPESignatureOK && g.isInBinariesFolder)
						?? exeProps.FirstOrDefault(g => g.isSimilarName && g.isInWinFolder)
						?? exeProps.FirstOrDefault(g => g.isSimilarName && g.isInBinariesFolder)
						?? exeProps.FirstOrDefault(g => g.isInWinFolder)
						?? exeProps.FirstOrDefault(g => g.isInBinariesFolder)
						?? exeProps.OrderBy(g => g.directoryCount).FirstOrDefault();

				if (bestProps != null) {
					// in Steam there are sometimes exes next to the shipping exe, like in Star Wars fallen order.
					// So we need to check if the exe is in the same folder as the shipping exe
					// However only some do apply, other are e.g. mod manager. So we need a positive list.
					if (bestProps.isShipping) {
						string folder = Path.GetDirectoryName(bestProps.filePath);

						var launcherProp = exeProps.FirstOrDefault(e => e.filePath != bestProps.filePath && Path.GetDirectoryName(e.filePath) == folder);
						if (launcherProp != null
							&& (settings.EXENameLauncher == null || settings.EXENameLauncher.Contains(Path.GetFileNameWithoutExtension(launcherProp.filePath)))) {
							Logger.Log.LogTrace($"{bestProps.filePath} has known launcher");
							bestProps = launcherProp;
						}
					}

					game.EXEPath = bestProps.filePath;

					Logger.Log.LogTrace($"{game.Name} best executable: {bestProps.filePath}");
				} else {
					Logger.Log.LogTrace($"No executable found for {game.Name}");
				}
			} catch (Exception ex) {
				Logger.Log.LogError(ex, $"Failed to find executable for {game.Name}");
			}
		}

#if DEBUGx
		// For testing purposes
		allGames.Add(new GameInstallation {
			Name = "Test App",
			StoreType = GameStoreType.GOG,
			GOGID = 1234567890,
			FolderPath = @"C:\Program Files\UEVRTestApp",
			EXEPath = @"C:\Program Files\UEVRTestApp\Binaries\Win64\UEVRTestApp-Win64-Shipping",
			IconURL = "/Assets/GOGLogo.jpg",
			ShellLaunchPath = "dummy"
		});
#endif

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

			string appDataPath = ReadRegistryValue(@"SOFTWARE\Epic Games\EpicGamesLauncher", "AppDataPath");
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
							|| IGNORE_GAME_NAMES.Contains(manifest.DisplayName, StringComparer.OrdinalIgnoreCase)) continue;  // e.g. "Unreal Engine"

						Logger.Log.LogTrace($"EPIC Game {manifest.DisplayName}");

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
			string steamInstallDir = ReadRegistryValue(@"SOFTWARE\Valve\Steam", "InstallPath");
			if (string.IsNullOrEmpty(steamInstallDir)) {
				Logger.Log.LogTrace("STEAM not installed");
				return allGames;
			}

			// there are two libraryfolders.vdf files. The one in \steamapps\ seems to be transient and only one drive,
			// while the global master sees to be \config\
			string vdfPath = Path.Join(steamInstallDir, "config", "libraryfolders.vdf");
			if (!File.Exists(vdfPath)) vdfPath = Path.Join(steamInstallDir, "steamapps", "libraryfolders.vdf");  // fallback
			if (!File.Exists(vdfPath)) throw new Exception("Steam Library Definition file not found");

			Logger.Log.LogTrace($"Reading Steam Library Definition {vdfPath}");

			var steamLibraryDefinition = VdfConvert.Deserialize(File.ReadAllText(vdfPath));
			foreach (var steamLibDirDefinition in steamLibraryDefinition.Value.Children<VProperty>()) {
				try {
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

									Logger.Log.LogTrace($"Steam Game {game.Name}");

									long lastPlayed = gameManifestDefinition.Value<long>("LastPlayed");
									if (lastPlayed > 0) game.LastPlayed = DateTimeOffset.FromUnixTimeSeconds(lastPlayed).DateTime;

									if (!IGNORE_GAME_NAMES.Contains(game.Name)) {
										game.FolderPath = Path.GetFullPath(Path.Join("steamapps", "common", relativeDirectoryName), vtoken_libPath);
										game.IconURL = $"https://cdn.cloudflare.steamstatic.com/steam/apps/{game.SteamID}/capsule_231x87.jpg"; //$"steam://install/{game.SteamID}";
																																			   // this does not allow to pass params $"steam://rungameid/{game.SteamID}";
																																			   // nohmd is required for practically all UEVR4 games
										game.ShellLaunchPath = $"\"{Path.Combine(steamInstallDir, "steam.exe")}\" -applaunch {game.SteamID} -nohmd";

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
				} catch (Exception ex) {
					// In case of a corrupted library definition
					Logger.Log.LogWarning($"Failed to read Steam Library {steamLibDirDefinition.Key}: {ex}");
				}
			}

			Logger.Log.LogTrace($"Found {allGames.Count} installed Steam games");
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
			string gogExeName = ReadRegistryValue(@"SOFTWARE\GOG.com\GalaxyClient", "clientExecutable");
			if (string.IsNullOrEmpty(gogExeName)) {
				Logger.Log.LogTrace("GOG not installed");
				return allGames;
			}

			Logger.Log.LogTrace($"Found GOG Client {gogExeName}");

			using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
			using var keyGames = hklm.OpenSubKey(@"SOFTWARE\GOG.com\Games", false);

			foreach (string subkeyName in keyGames.GetSubKeyNames()) {
				try {
					using var gameKey = keyGames.OpenSubKey(subkeyName, false);

					string gameName = gameKey.GetValue("gameName") as string;
					if (string.IsNullOrEmpty(gameName) || IGNORE_GAME_NAMES.Contains(gameName, StringComparer.OrdinalIgnoreCase)) continue;

					Logger.Log.LogTrace($"GOG Game {gameName}");
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
				} catch (Exception ex) {
					// In case of a corrupted library definition
					Logger.Log.LogWarning($"Failed to read XBox game key {subkeyName}: {ex}");
				}
			}

			Logger.Log.LogTrace($"Found {allGames.Count} installed GOG games");
		} catch (Exception ex) {
			// Show must go on if an installation of one store is flawed
			Logger.Log.LogCritical(ex, "Failed to scan GOG");
		}

		return allGames.OrderBy(g => g.Name).ToList();
	}
	#endregion

	#region FindAllXBoxGames
	static List<GameInstallation> FindAllXBoxGames() {
		var allGames = new List<GameInstallation>();

		try {
			using var packageRootsKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\GamingServices\PackageRepository\Root");
			if (packageRootsKey == null) {
				// Normal if no games are installed
				Logger.Log.LogTrace("GamingServices PckageRoot not found");
				return allGames;
			}

			using var userPackagesKey = Registry.CurrentUser.OpenSubKey(
				@"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages");

			var packageManager = new PackageManager();

			foreach (string packageKey in packageRootsKey.GetSubKeyNames()) {
				using var packageSubKey = packageRootsKey.OpenSubKey(packageKey);
				if (packageSubKey == null) continue;

				var childSubKeys = packageSubKey.GetSubKeyNames();
				if (childSubKeys.Length == 0) continue;

				using (var subChildKey = packageSubKey.OpenSubKey(childSubKeys[0])) {
					if (subChildKey == null) continue;
					string packageId = subChildKey.GetValue("Package") as string;
					string rootPath = subChildKey.GetValue("Root") as string;

					if (string.IsNullOrEmpty(packageId) || string.IsNullOrEmpty(rootPath))
						continue;

					var game = new GameInstallation {
						XBoxID = packageId,
						FolderPath = Path.GetDirectoryName(rootPath),
						StoreType = GameStoreType.XBox
					};

					// DOS style folder name sometime disturb in other function
					/* if (game.FolderPath.StartsWith(@"\\.\") || game.FolderPath.StartsWith(@"\\?\"))
						game.FolderPath = game.FolderPath[4..]; */

					// Get the AppID from the PackageID (no versions)
					var package = packageManager.FindPackageForUser(string.Empty, packageId);
					if (package != null) {
						string manifestPath = Path.Combine(package.InstalledLocation.Path, "AppxManifest.xml");
						var manifest = XDocument.Parse(File.ReadAllText(manifestPath));

						string applicationId = manifest.Descendants()
											   .Where(e => e.Name.LocalName == "Application")
											   .Select(app => app.Attribute("Id")?.Value)
											   .FirstOrDefault() ?? "App";

						game.ShellLaunchPath = $"shell:AppsFolder\\{package.Id.FamilyName}!{applicationId}";
					}

					/* Often not a real logo of the game
					game.IconURL=Path.Combine(game.FolderPath, "GraphicsLogo.png");
					if (!File.Exists(game.IconURL)) Path.Combine(game.FolderPath, "StoreLogo.png");
					if (!File.Exists(game.IconURL)) */
					game.IconURL = "/Assets/XBoxLogo.png";

					// Get display name from packages key
					using (var pkgKey = userPackagesKey?.OpenSubKey(packageId)) {
						game.Name = (pkgKey?.GetValue("DisplayName") as string)
																		?? game.EXEName;
					}

					// Skip well-known non-game entries
					if (string.IsNullOrEmpty(game.Name) || IGNORE_GAME_NAMES.Contains(game.Name, StringComparer.OrdinalIgnoreCase)) continue;

					allGames.Add(game);
					Logger.Log.LogTrace($"Xbox Game: {game.Name} at {game.FolderPath}");
				}
			}

			Logger.Log.LogTrace($"Found {allGames.Count} installed Xbox games");
		} catch (Exception ex) {
			Logger.Log.LogCritical(ex, "Failed to scan Xbox games");
		}

		return allGames;
	}
	#endregion

	#region FindAllEAGames
	static List<GameInstallation> FindAllEAGames() {
		var allGames = new List<GameInstallation>();

		try {
			// No way to find the root installation dir. Simple search all drivers for the common name.
			var drives = DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed);

			foreach (var drive in drives) {
				string programDir = Path.Combine(drive.RootDirectory.FullName, "Program Files\\EA Games");
				if (!Directory.Exists(programDir)) continue;

				foreach (string gameDir in Directory.GetDirectories(programDir, "*", SearchOption.TopDirectoryOnly)) {
					string installerPath = Path.Combine(gameDir, "__Installer\\installerdata.xml");
					if (!File.Exists(installerPath)) continue;

					Logger.Log.LogTrace($"EA App installed in {gameDir}");

					var installerData = XDocument.Parse(File.ReadAllText(installerPath));
					var game = new GameInstallation {
						StoreType = GameStoreType.EA,
						Name = installerData.Descendants()
							.Where(e => e.Name.LocalName == "gameTitle").First().Value.Trim(),  // typically US
						FolderPath = gameDir,
						IconURL = "/Assets/EALogo.jpg"
					};

					// Skip well-known non-game entries
					if (IGNORE_GAME_NAMES.Contains(game.Name, StringComparer.OrdinalIgnoreCase)) continue;

					game.EAContentIDs = installerData.Descendants()
						.Where(e => e.Name.LocalName == "contentIDs").First().Elements().Select(e => e.Value.Trim()).Order().ToArray();

					game.ShellLaunchPath = "origin2://game/launch/?offerIds=" + string.Join(',', game.EAContentIDs);

					allGames.Add(game);
				}
			}

			Logger.Log.LogTrace($"Found {allGames.Count} installed EA games");
		} catch (Exception ex) {
			Logger.Log.LogCritical(ex, "Failed to scan EA games");
		}

		return allGames.OrderBy(g => g.Name).ToList();
	}
	#endregion

	#region FindAllWindowsUninstalls
	/// <summary>Typical Windows uninstall entries. Fallback option.</summary>
	static List<GameInstallation> FindAllWindowsUninstalls() {
		var allGames = new List<GameInstallation>();

		try {
			string uninstallRoot = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

			// Check both registry views (32-bit and 64-bit)
			foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 }) {
				using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
				using var uninstallKey = hklm.OpenSubKey(uninstallRoot, false);
				if (uninstallKey == null) continue;

				foreach (string subkeyName in uninstallKey.GetSubKeyNames()) {
					try {
						using var appKey = uninstallKey.OpenSubKey(subkeyName, false);
						if (appKey == null) continue;

						string publisher = appKey.GetValue("Publisher") as string;
						string displayName = appKey.GetValue("DisplayName") as string;
						string installLocation = appKey.GetValue("InstallLocation") as string;
						if (string.IsNullOrEmpty(publisher) || string.IsNullOrEmpty(installLocation) || string.IsNullOrEmpty(displayName)
							|| !ALLOWED_PUBLISHERS_CONTAINS.Any(p => publisher.Contains(p, StringComparison.OrdinalIgnoreCase))
							|| IGNORE_GAME_NAMES.Contains(displayName, StringComparer.OrdinalIgnoreCase)) continue;

						if (!Directory.Exists(installLocation)) {
							Logger.Log.LogWarning($"Uninstall entry {displayName} has no valid install location {installLocation}");
							continue;
						}

						var game = new GameInstallation {
							StoreType = GameStoreType.Windows,
							Name = displayName,
							FolderPath = installLocation,
							IconURL = "/Assets/GenericGameLogo.jpg"
						};

						allGames.Add(game);
						Logger.Log.LogTrace($"Windows Game: {game.Name} at {game.FolderPath}");
					} catch (Exception ex) {
						Logger.Log.LogWarning(ex, $"Failed to read uninstall key {subkeyName}");
					}
				}
			}

			Logger.Log.LogTrace($"Found {allGames.Count} in Windows Uninstall");
		} catch (Exception ex) {
			Logger.Log.LogCritical(ex, "Failed to scan Windows uninstall entries");
		}

		return allGames.OrderBy(g => g.Name).ToList();
	}
	#endregion

	#region * Helpers
	/// <summary>Read registry value, checking both 32-bit and 64-bit views</summary>
	static string ReadRegistryValue(string keyPath, string valueName) {
		string result = ReadRegistryValueFromView(keyPath, valueName, RegistryView.Registry32);
		if (result != null) return result;

		return ReadRegistryValueFromView(keyPath, valueName, RegistryView.Registry64);
	}

	static string ReadRegistryValueFromView(string keyPath, string valueName, RegistryView view) {
		using (var regRoot = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
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
		GameStoreType.XBox => $"X{XBoxID}",
		GameStoreType.EA => $"EA{string.Join('|', EAContentIDs)}",
		GameStoreType.Windows => $"W{FolderPath}",
		_ => throw new NotImplementedException()
	};

	public long? SteamID { get; set; }

	public long? GOGID { get; set; }

	public string EpicID { get; set; }
	public string EpicNamespace { get; set; }

	public string XBoxID { get; set; }

	public string[] EAContentIDs { get; set; }

	public string Name { get; set; }

	/// <summary>Games root folder (EXE is typically in a sub folder)</summary>
	public string FolderPath { get; set; }

	public string IconURL { get; set; }

	public GameStoreType StoreType { get; set; }

	/// <summary>Full path to EXE</summary>
	public string EXEPath { get; set; }

	/// <summary>The real executable (not the launchers above) without .exe</summary>
	public string EXEName => EXEPath != null ? Path.GetFileNameWithoutExtension(EXEPath) : null;

	public string ShellLaunchPath { get; set; }

	public DateTime? LastPlayed { get; set; }
}

public enum GameStoreType {
	Steam,
	Epic,
	GOG,
	XBox,
	EA,
	Windows
}

/// <summary>Disk representation.</summary>
internal class GameInstallationCache {
	public const int LATEST_CACHE_STRUCTURE_VERSION = 2;

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
	public bool isInWinFolder, isInBinariesFolder, isShipping, isSimilarName, isPESignatureOK;
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
