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
#endregion

namespace UEVRDeluxe.Code;

public static class GameStoreManager {
	readonly static string[] IGNORE_GAME_NAMES = ["Steamworks Common Redistributables", "SteamVR"];

	readonly static string[] UNREAL_ENGINE_STRINGS = ["UnrealEngine", "UE4", "UE5", "UE6", "Epic Games"];

	public static List<GameInstallation> FindAllUEVRGames() {
		var allGames = new List<GameInstallation>();

		allGames.AddRange(FindAllSteamUEVRGames());

		// Find UE-Executable. This is more an art than a science ;-)
		foreach (var game in allGames) {
			string[] exesPaths = Directory.GetFiles(game.FolderPath, "*.exe", SearchOption.AllDirectories);

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
				game.EXEName = Path.GetFileNameWithoutExtension(bestProps.filePath);

				if (!bestProps.isShipping) {
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



	static List<GameInstallation> FindAllSteamUEVRGames() {
		var allGames = new List<GameInstallation>();

		// Find Steam Root dir
		string steamInstallDir = string.Empty;

		using (var regRoot = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
		using (var regSteam = regRoot.OpenSubKey(Path.Join("SOFTWARE", "Valve", "Steam"), false)) {
			if (regSteam == null) return allGames;

			var installPath = regSteam.GetValue("InstallPath");
			if (installPath == null) return allGames;

			var installPathKind = regSteam.GetValueKind("InstallPath");
			switch (regSteam.GetValueKind("InstallPath")) {
				case RegistryValueKind.String:
				case RegistryValueKind.ExpandString:
					steamInstallDir = (string)installPath;
					break;
				default:
					return allGames;
			}
		}

		if (!File.Exists(Path.Join(steamInstallDir, "steam.exe"))) return allGames;

		var vdfPath = Path.Join(steamInstallDir, "steamapps", "libraryfolders.vdf");
		if (!File.Exists(vdfPath)) {
			Debug.WriteLine("Steam Library Definition file not found");
			return allGames;
		}

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

							allGames.Add(game);
						}
					}
				}
			}
		}

		return allGames.OrderByDescending(g => g.LastPlayed ?? new DateTime()).ThenBy(g => g.Name).ToList();
	}
}

public class GameInstallation {
	public long SteamID { get; set; }
	public string Name { get; set; }

	public string FolderPath { get; set; }

	public string IconURL { get; set; }

	public GameStoreType StoreType { get; set; }

	/// <summary>The real executable (not the launchers above) without .exe</summary>
	public string EXEName { get; set; }

	public DateTime? LastPlayed { get; set; }
}

public enum GameStoreType {
	Steam,
	EpicGames,
	Origin,
	UPlay
}

internal class ExecutableProp {
	public string filePath;
	public bool isInWinFolder, isInBinariesFolder, isShipping;
	public int directoryCount;
}
