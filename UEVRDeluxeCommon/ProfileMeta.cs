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

	/// <summary>Minimum version of UEVR, given as day of UEVR code to accomodate from interims releases.</summary>
	/// <remarks>1.05 was 2024-10-31</remarks>
	[JsonPropertyName("minUEVRVersionDay")]
	[Obsolete()]
	public DateTime? MinUEVRVersionDate { get; set; }

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

		if (MinUEVRVersionDate.HasValue)
			return $"{FILENAME}: minUEVRVersionDate is obsolete, use minUEVRNightlyNumber/maxUEVRNightlyNumber instead";

		if ((MinUEVRNightlyNumber.HasValue && MaxUEVRNightlyNumber.HasValue && MinUEVRNightlyNumber > MaxUEVRNightlyNumber)
			|| MinUEVRNightlyNumber <= 0 || MaxUEVRNightlyNumber <= 0)
			return $"{FILENAME}: Invalid Min/Max UEVR nightly number";

		if (ModifiedDate.Year < 2023 || ModifiedDate.Date > DateTime.Today) return $"{FILENAME}: Invalid ModifiedDate";

		if (!string.IsNullOrWhiteSpace(Remarks) && (Remarks.Trim() != Remarks || Remarks.Length > TEXTFIELDS_MAX_LENGTH))
			return $"{FILENAME}: Invalid Remarks";

		return null;
	}
}
