#region Usings
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Globalization;
using Windows.Media.SpeechRecognition;
#endregion

namespace UEVRDeluxe.Code;

public class VoiceCommandRecognizer {
	// Replaced SpeechRecognitionEngine with SpeechRecognizer
	SpeechRecognizer recognizer;
	string exeName;

	// Added fields for mapping phrases to commands
	Dictionary<string, int> commandMap;

	public async Task StartAsync(string exeName) {
		this.exeName = exeName;

		// Stop any ongoing recognition session
		await StopAsync();

		string profilePath = VoiceCommandProfile.GetFilePath(exeName);
		if (!File.Exists(profilePath)) {
			Logger.Log.LogTrace($"No voice profile for {exeName}");
			return;
		}

		var profile = JsonSerializer.Deserialize<VoiceCommandProfile>(File.ReadAllText(profilePath),
			new JsonSerializerOptions { PropertyNameCaseInsensitive = true });


		// Build a list of expected phrases (they must start with the keyword)
		commandMap = new Dictionary<string, int>();

		var phraseList = new List<string>();

		if (profile.Commands?.Any() ?? false) {
			foreach (var command in profile.Commands) {
				string phrase = command.Text.Trim();
				phraseList.Add(phrase);
				commandMap[phrase.ToLowerInvariant()] = command.VKKeyCode;
			}
		}
		var listConstraint = new SpeechRecognitionListConstraint(phraseList, "commands");

		if (string.IsNullOrWhiteSpace(profile.LanguageTag))
			throw new Exception("LanguageTag is empty in voice profile");

		var language = new Language(profile.LanguageTag);

		recognizer = new SpeechRecognizer(language);
		recognizer.Constraints.Add(listConstraint);

		var compilationResult = await recognizer.CompileConstraintsAsync();
		if (compilationResult.Status != SpeechRecognitionResultStatus.Success) {
			Logger.Log.LogError("Failed to compile speech recognition constraints");
			return;
		}

		recognizer.ContinuousRecognitionSession.ResultGenerated += (sender, args) => {
			Recognizer_SpeechRecognized(args.Result);
		};

		await recognizer.ContinuousRecognitionSession.StartAsync();
	}

	public async Task StopAsync() {
		if (recognizer != null) {
			await recognizer.ContinuousRecognitionSession.StopAsync();
			recognizer.Dispose();
			recognizer = null;
		}
	}

	void Recognizer_SpeechRecognized(SpeechRecognitionResult result) {
		if (result.Status == SpeechRecognitionResultStatus.Success) {
			var recognizedText = result.Text.ToLowerInvariant();
			Logger.Log.LogTrace($"Command recognized: {recognizedText}");

			if (commandMap.TryGetValue(recognizedText, out int vk)) {
				// Check if the foreground window belongs to the game process
				IntPtr foregroundWindow = Win32.GetForegroundWindow();
				Win32.GetWindowThreadProcessId(foregroundWindow, out uint foregroundProcessId);
				var process = Process.GetProcessById((int)foregroundProcessId);

				if (process.ProcessName.Equals(exeName, StringComparison.OrdinalIgnoreCase)) {
					// Simulate a key press and release
					var inputs = new INPUT[] {
						new() {
							type = Win32.INPUT_KEYBOARD,
							u = new InputUnion {
								ki = new KEYBDINPUT {
									wVk = (ushort)vk
								}
							}
						},
						new() {
							type = Win32.INPUT_KEYBOARD,
							u = new InputUnion {
								ki = new KEYBDINPUT {
									wVk = (ushort)vk,
									dwFlags = Win32.KEYEVENTF_KEYUP
								}
							}
						}
					};

					Win32.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
				}
			}
		}
	}
}

#region * VoiceCommandProfile
public class VoiceCommandProfile {
	/// <summary>E.g. "en-us"</summary>
	public string LanguageTag { get; set; }

	/// <summary>The real executable (not the launchers) without .exe</summary>
	public string EXEName { get; set; }

	/// <summary>Commands (may include keywords)</summary>
	public List<VoiceCommand> Commands { get; set; }

	public static string GetFilePath(string exeName) {
		string rootDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "UnrealVRMod");

		if (!Directory.Exists(rootDir)) Directory.CreateDirectory(rootDir);
		string directory = Path.Combine(rootDir, "VoiceCommandProfiles");
		if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

		directory = Path.Combine(directory, $"VoiceCommands_{exeName}.json");

		return directory;
	}
}

public class VoiceCommand {
	/// <summary>The spoken text (may contain multiple words)</summary>
	public string Text { get; set; }

	/// <summary>Virtual keyboard code</summary>
	public int VKKeyCode { get; set; }
}
#endregion