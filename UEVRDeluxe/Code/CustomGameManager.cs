using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace UEVRDeluxe.Code;

/// <summary>Locally registered, non-store game executables.</summary>
internal static class CustomGameManager {
    static string DataFile => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "UEVRDeluxe", "CustomGames.json");

    public static List<GameInstallation> Load() {
        if (!File.Exists(DataFile)) return new();

        var games = JsonSerializer.Deserialize<List<GameInstallation>>(
            File.ReadAllText(DataFile)) ?? new();

        return games.Where(g => g != null
                && g.StoreType == GameStoreType.Custom
                && !string.IsNullOrWhiteSpace(g.CustomID)
                && !string.IsNullOrWhiteSpace(g.EXEPath)
                && File.Exists(g.EXEPath))
            .GroupBy(g => g.EXEPath, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
    }

    public static GameInstallation Add(string exePath) {
        if (string.IsNullOrWhiteSpace(exePath))
            throw new ArgumentException("Select a game executable.", nameof(exePath));

        string fullPath = Path.GetFullPath(exePath);
        if (!File.Exists(fullPath) ||
            !string.Equals(Path.GetExtension(fullPath), ".exe", StringComparison.OrdinalIgnoreCase))
            throw new FileNotFoundException("Select an existing Windows .exe game file.", fullPath);

        var games = Load();
        var existing = games.FirstOrDefault(g =>
            string.Equals(g.EXEPath, fullPath, StringComparison.OrdinalIgnoreCase));
        if (existing != null) return existing;

        string name = Path.GetFileNameWithoutExtension(fullPath);
        if (name.EndsWith("-Win64-Shipping", StringComparison.OrdinalIgnoreCase))
            name = name[..^"-Win64-Shipping".Length];
        else if (name.EndsWith("-WinGDK-Shipping", StringComparison.OrdinalIgnoreCase))
            name = name[..^"-WinGDK-Shipping".Length];

        var game = new GameInstallation {
            CustomID = Guid.NewGuid().ToString("N"),
            StoreType = GameStoreType.Custom,
            Name = name,
            FolderPath = Path.GetDirectoryName(fullPath),
            EXEPath = fullPath,
            ShellLaunchPath = $"\"{fullPath}\"",
            IconURL = "/Assets/GenericGameLogo.jpg"
        };

        games.Add(game);
        Directory.CreateDirectory(Path.GetDirectoryName(DataFile)!);

        string tempFile = DataFile + ".tmp";
        File.WriteAllText(tempFile, JsonSerializer.Serialize(games,
            new JsonSerializerOptions { WriteIndented = true }));
        File.Move(tempFile, DataFile, true);

        return game;
    }
}
