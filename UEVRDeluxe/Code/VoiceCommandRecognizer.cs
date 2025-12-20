#region Usings
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Speech.Recognition;
using System.Text.Json;
#endregion

namespace UEVRDeluxe.Code;

public class VoiceCommandRecognizer {
	SpeechRecognitionEngine recognizer;
	string exeName;

	Dictionary<string, int> mapCommand2KeyCode;
	float minConfidence;
	bool stopAfterInjected;

	const int DUMMYKEYCODE_INJECT = 0xffffff;

	/// <summary>For Testing</summary>
	public event Action<object, string, float> SpeechRecognized;

	/// <summary>No keypress, but injection</summary>
	public event Action InjectRequested;

	/// <summary>Start without file/exe integrtation</summary>
	public void Start(VoiceCommandProfile profile) {
		Stop();
		if (SpeechRecognized == null) throw new Exception("No event handler for SpeechRecognized");
		StartWorker(profile);
	}

	public void Start(string exeName) {
		this.exeName = exeName;

		Stop();

		string profilePath = VoiceCommandProfile.GetFilePath(exeName);
		if (!File.Exists(profilePath)) {
			Logger.Log.LogTrace($"No voice profile for {exeName}");
			return;
		}

		var profile = JsonSerializer.Deserialize<VoiceCommandProfile>(File.ReadAllText(profilePath),
			new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

		StartWorker(profile);
	}

	void StartWorker(VoiceCommandProfile profile) {
		minConfidence = profile.MinConfidence;

		bool hasCommands = profile.Commands?.Any() ?? false;
		stopAfterInjected = !hasCommands && !string.IsNullOrWhiteSpace(profile.InjectText);

		// Build a list of expected phrases (they must start with the keyword)
		mapCommand2KeyCode = new();

		var phraseList = new List<string>();

		if (profile.Commands?.Any() ?? false) {
			foreach (var command in profile.Commands) {
				string phrase = command.Text.Trim();
				phraseList.Add(phrase);
				mapCommand2KeyCode[phrase.ToLowerInvariant()] = command.VKKeyCode;
			}
		}

		if (!string.IsNullOrWhiteSpace(profile.InjectText)) {
			// Add the inject text as a command
			phraseList.Add(profile.InjectText.Trim());
			mapCommand2KeyCode[profile.InjectText.Trim().ToLowerInvariant()] = DUMMYKEYCODE_INJECT;
		}

		if (string.IsNullOrWhiteSpace(profile.LanguageTag))
			throw new Exception("LanguageTag is empty in voice profile");

		recognizer = new SpeechRecognitionEngine(new System.Globalization.CultureInfo(profile.LanguageTag));

		var grammarBuilder = new GrammarBuilder();
		grammarBuilder.Culture = recognizer.RecognizerInfo.Culture;
		grammarBuilder.Append(new Choices(phraseList.ToArray()));
		var grammar = new Grammar(grammarBuilder);

		recognizer.LoadGrammar(grammar);

		recognizer.SpeechRecognized += Recognizer_SpeechRecognized;

		recognizer.SetInputToDefaultAudioDevice();
		recognizer.RecognizeAsync(RecognizeMode.Multiple);
	}

	public void Stop() {
		if (recognizer != null) {
			Logger.Log.LogTrace("Stopping voice recognition");
			recognizer.RecognizeAsyncCancel();
			recognizer.Dispose();
			recognizer = null;
		}
	}

	void Recognizer_SpeechRecognized(object sender, SpeechRecognizedEventArgs args) {
		if (args.Result == null) return;

		var recognizedText = args.Result.Text.ToLowerInvariant();
		Logger.Log.LogTrace($"Command recognized: {recognizedText} ({args.Result.Confidence:p1})");

		if (args.Result.Confidence < minConfidence) return;

		// Fire event for any listeners (used by test feature)
		SpeechRecognized?.Invoke(this, recognizedText, args.Result.Confidence);

		if (!string.IsNullOrEmpty(exeName) && mapCommand2KeyCode.TryGetValue(recognizedText, out int vk)) {
			// Check if the foreground window belongs to the game process
			IntPtr foregroundWindow = Win32.GetForegroundWindow();
			Win32.GetWindowThreadProcessId(foregroundWindow, out uint foregroundProcessId);
			var process = Process.GetProcessById((int)foregroundProcessId);

			if (process.ProcessName.Equals(exeName, StringComparison.OrdinalIgnoreCase)) {
				if (vk == DUMMYKEYCODE_INJECT) {
					InjectRequested?.Invoke();
					if (stopAfterInjected) Stop();
				} else {
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

	/// <summary>Minimum confidence for a command being recognized (0..1)</summary>
	public float MinConfidence { get; set; }

	/// <summary>Text to say to inject (for late injection scenarios)</summary>
	public string InjectText { get; set; }

	/// <summary>Commands (may include keywords)</summary>
	public List<VoiceCommand> Commands { get; set; }

	public static string GetFilePath(string exeName) {
		string rootDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "UEVRDeluxe");

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