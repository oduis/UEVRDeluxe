using System;
using System.IO;
using System.Text.Json.Serialization;

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

	/// <summary>Minimum version of UEVR, given as day to accomodate from interims releases.</summary>
	/// <remarks>1.05 was 2024-11-16</remarks>
	[JsonPropertyName("minUEVRVersionDay")]
	public DateTime MinEVRVersionDate { get; set; }

	[JsonIgnore]
	public string MinUEVRVersionDateDisplay => MinEVRVersionDate.ToString("d");

	/// <summary>Indicates whether to nullify plugins</summary>
	[JsonPropertyName("nullifyPlugins")]
	public bool NullifyPlugins { get; set; }

	[JsonPropertyName("modifiedDate")]
	public DateTime ModifiedDate { get; set; }

	[JsonIgnore]
	public string ModifiedDateDisplay => ModifiedDate.ToString("d");


	/// <summary>SHORT remarks (max 128 chars)</summary>
	[JsonPropertyName("remarks")]
	public string Remarks { get; set; }

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

		if (MinEVRVersionDate.Year < 2023) return $"{FILENAME}: Invalid UEVRVersionDate";

		if (ModifiedDate.Year < 2023 || ModifiedDate.Date > DateTime.Today) return $"{FILENAME}: Invalid ModifiedDate";

		if (!string.IsNullOrWhiteSpace(Remarks) && (Remarks.Trim() != Remarks || Remarks.Length > TEXTFIELDS_MAX_LENGTH))
			return $"{FILENAME}: Invalid Remarks";

		return null;
	}

}
