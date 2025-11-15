#region Usings
using System;
using System.IO;
using System.Text.Json.Serialization;
using System.Collections.Generic; 
#endregion

namespace UEVRDeluxe.Common;

/// <summary>Metadata of a game profile</summary>
public class ProfileMeta {
	public const string FILENAME = "ProfileMeta.json";
	public const string DESCRIPTION_FILENAME = "ProfileDescription.md";

	public const int TEXTFIELDS_MAX_LENGTH = 128;

	/// <summary>Server generated random ID to identify a profile</summary>
	public Guid ID { get; set; }

	/// <summary>Name of execuable file of the game, without extension</summary>
	[JsonPropertyName("exeName")]
	public string EXEName { get; set; }

	/// <summary>Name of the game, typically defined by Steam</summary>
	[JsonPropertyName("gameName")]
	public string GameName { get; set; }

	/// <summary>Optional</summary>
	[JsonPropertyName("gameVersion")]
	public string GameVersion { get; set; }

	[JsonPropertyName("authorName")]
	public string AuthorName { get; set; }

	/// <summary>If set, minimum version number</summary>
	[JsonPropertyName("minUEVRNightlyNumber")]
	public int? MinUEVRNightlyNumber { get; set; }

	/// <summary>If set, maximum version number</summary>
	[JsonPropertyName("maxUEVRNightlyNumber")]
	public int? MaxUEVRNightlyNumber { get; set; }

	[JsonIgnore]
	public string MinMaxUEVRNightlyNumberText {
		get {
			if (MinUEVRNightlyNumber.HasValue && MaxUEVRNightlyNumber.HasValue)
				return $"{MinUEVRNightlyNumber} to {MaxUEVRNightlyNumber}";

			if (MinUEVRNightlyNumber.HasValue && !MaxUEVRNightlyNumber.HasValue)
				return $"{MinUEVRNightlyNumber} or higher";

			if (!MinUEVRNightlyNumber.HasValue && MaxUEVRNightlyNumber.HasValue)
				return $"{MaxUEVRNightlyNumber} or lower";

			return "( no restriction )";
		}
	}

	/// <summary>Indicates whether to nullify plugins</summary>
	[JsonPropertyName("nullifyPlugins")]
	public bool NullifyPlugins { get; set; }

	/// <summary>If the game may not be injected directly, but only after it was started and in 3D mode</summary>
	/// <remarks>e.g. Star Wars Fallen Order has this problem, leading to black windows if injected on start</remarks>
	[JsonPropertyName("lateInjection")]
	public bool LateInjection { get; set; }

	[JsonPropertyName("modifiedDate")]
	public DateTime ModifiedDate { get; set; }

	/// <summary>SHORT remarks (max 128 chars)</summary>
	[JsonPropertyName("remarks")]
	public string Remarks { get; set; }

	/// <summary>Files that should be copied from the profile to the game folder on install (typically PAK files)</summary>
	/// <remarks>They will be removed on uninstall</remarks>
	[JsonPropertyName("fileCopies")]
	public List<FileCopy> FileCopies { get; set; }

	public string Check() {
		if (string.IsNullOrWhiteSpace(EXEName) || EXEName.Trim() != EXEName || EXEName.Length > TEXTFIELDS_MAX_LENGTH
			|| Path.GetFileNameWithoutExtension(EXEName) != Path.GetFileName(EXEName))
			return $"{FILENAME}: Invalid EXEName";

		if (string.IsNullOrWhiteSpace(GameName) || GameName.Trim() != GameName || GameName.Length > TEXTFIELDS_MAX_LENGTH * 2)
			return $"{FILENAME}: Invalid GameName";

		if (!string.IsNullOrWhiteSpace(GameVersion) && (GameVersion.Trim() != GameVersion || GameVersion.Length > TEXTFIELDS_MAX_LENGTH))
			return $"{FILENAME}: Invalid GameVersion";

		if (string.IsNullOrWhiteSpace(AuthorName) || AuthorName.Trim() != AuthorName || AuthorName.Length > TEXTFIELDS_MAX_LENGTH)
			return $"{FILENAME}: Invalid AuthorName";

		if ((MinUEVRNightlyNumber.HasValue && MaxUEVRNightlyNumber.HasValue && MinUEVRNightlyNumber > MaxUEVRNightlyNumber)
			|| MinUEVRNightlyNumber <= 0 || MaxUEVRNightlyNumber <= 0)
			return $"{FILENAME}: Invalid Min/Max UEVR nightly number";

		if (ModifiedDate.Year < 2023 || ModifiedDate.Date > DateTime.Today) return $"{FILENAME}: Invalid ModifiedDate";

		if (!string.IsNullOrWhiteSpace(Remarks) && (Remarks.Trim() != Remarks || Remarks.Length > TEXTFIELDS_MAX_LENGTH))
			return $"{FILENAME}: Invalid Remarks";

		// Validate FileCopies list if present: only check format, not filesystem
		if (FileCopies != null) {
			for (int i = 0; i < FileCopies.Count; i++) {
				var fc = FileCopies[i];
				if (fc == null) return $"{FILENAME}: Invalid FileCopies: entry is null";

				// Basic shared validations: non-empty and trimmed
				if (string.IsNullOrWhiteSpace(fc.SourceFileRelProfile) || fc.SourceFileRelProfile.Trim() != fc.SourceFileRelProfile)
					return $"{FILENAME}: Invalid FileCopies.SourceFileRelProfile: empty or not trimmed";

				if (string.IsNullOrWhiteSpace(fc.DestinationFolderRelGameEXE) || fc.DestinationFolderRelGameEXE.Trim() != fc.DestinationFolderRelGameEXE)
					return $"{FILENAME}: Invalid FileCopies.DestinationFolderRelGameEXE: empty or not trimmed";

				char[] invalidPathChars = Path.GetInvalidPathChars();
				char[] invalidFileNameChars = Path.GetInvalidFileNameChars();

				if (fc.SourceFileRelProfile.IndexOfAny(invalidPathChars) >= 0)
					return $"{FILENAME}: Invalid FileCopies.SourceFileRelProfile: contains invalid path characters";

				if (Path.GetFileName(fc.SourceFileRelProfile).IndexOfAny(invalidFileNameChars) >= 0)
					return $"{FILENAME}: Invalid FileCopies.SourceFileRelProfile: filename contains invalid characters";

				if (fc.DestinationFolderRelGameEXE.IndexOfAny(invalidPathChars) >= 0)
					return $"{FILENAME}: Invalid FileCopies.DestinationFolderRelGameEXE: contains invalid path characters";

				if (Path.GetFileName(fc.DestinationFolderRelGameEXE).IndexOfAny(invalidFileNameChars) >= 0)
					return $"{FILENAME}: Invalid FileCopies.DestinationFolderRelGameEXE: folder name contains invalid characters";

				// Source should represent a file path (may be relative): it must not end with a directory separator
				if (fc.SourceFileRelProfile.EndsWith(Path.DirectorySeparatorChar) || fc.SourceFileRelProfile.EndsWith(Path.AltDirectorySeparatorChar))
					return $"{FILENAME}: Invalid FileCopies.SourceFileRelProfile: appears to be a directory, should be a file";

				// Heuristic: destination should represent a folder path (may be relative). The last segment should not look like a file with an extension.
				string destLast = Path.GetFileName(fc.DestinationFolderRelGameEXE.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
				if (!string.IsNullOrEmpty(destLast) && !string.IsNullOrEmpty(Path.GetExtension(destLast)))
					return $"{FILENAME}: Invalid FileCopies.DestinationFolderRelGameEXE: appears to be a file (has extension), should be a folder";
			}
		}

		return null;
	}
}

/// <summary>Defines a file copy action from the profile to the game folder, typically used for PAK files</summary>
public class FileCopy {
	/// <summary>Path relative to profile of the source file to copy</summary>
	[JsonPropertyName("sourceFileRelProfile")]
	public string SourceFileRelProfile { get; set; }

	/// <summary>Folder relative to the folder of the game EXE where the file should be copied to</summary>
	[JsonPropertyName("destinationFolderRelGameEXE")]
	public string DestinationFolderRelGameEXE { get; set; }
}
