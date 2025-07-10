#region Usings
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using UEVRDeluxe.Code;
#endregion

namespace UEVRDeluxe.ViewModels;

public class GamePageVM : VMBase {
	GameInstallation gameInstallation;
	public GameInstallation GameInstallation { get => gameInstallation; set => Set(ref gameInstallation, value); }

	LocalProfile localProfile;
	public LocalProfile LocalProfile {
		get => localProfile;
		set => Set(ref localProfile, value, [nameof(VisibleIfProfile), nameof(VisibleIfNoProfile),
			nameof(UEVRVersionWarning), nameof(UEVRVersionWarningVisible),
			nameof(ProfileMetaVisible), nameof(ProfileDescriptionVisible), nameof(Warning), nameof(VisibleLateInjectWarning)]);
	}

	int? currentUEVRNightlyNumber;
	public int? CurrentUEVRNightlyNumber {
		get => currentUEVRNightlyNumber;
		set => Set(ref currentUEVRNightlyNumber, value,
			[nameof(UEVRVersionWarning), nameof(UEVRVersionWarningVisible)]);
	}

	public string UEVRVersionWarning {
		get {
			if (currentUEVRNightlyNumber.HasValue &&
				((LocalProfile?.Meta?.MinUEVRNightlyNumber ?? int.MinValue) > CurrentUEVRNightlyNumber
				|| (LocalProfile?.Meta?.MaxUEVRNightlyNumber ?? int.MaxValue) < CurrentUEVRNightlyNumber))
				return $"Currently installed UEVR backend version {CurrentUEVRNightlyNumber} might not be compatible";

			return null;
		}
	}
	public Visibility UEVRVersionWarningVisible => !string.IsNullOrEmpty(UEVRVersionWarning) ? Visibility.Visible : Visibility.Collapsed;


	string currentOpenXRRuntime;
	public string CurrentOpenXRRuntime { get => currentOpenXRRuntime; set => Set(ref currentOpenXRRuntime, value); }

	public Visibility ProfileMetaVisible => Warning == null && !string.IsNullOrEmpty(LocalProfile?.Meta?.EXEName) ? Visibility.Visible : Visibility.Collapsed;

	public Visibility ProfileDescriptionVisible => Warning == null && !string.IsNullOrWhiteSpace(LocalProfile?.DescriptionMD) ? Visibility.Visible : Visibility.Collapsed;

	public Visibility WarningVisible => Warning != null ? Visibility.Visible : Visibility.Collapsed;

	public string Warning {
		get {
			if (localProfile == null)
				return "You have no UEVR Profile locally installed for this game\r\n- Try \"Search profile\" (recommended) or\r\n- add a new profile yourself";

			if (string.IsNullOrEmpty(localProfile.Meta.EXEName))
				return "You have a local profile for this game, but it contains no publishing description";

			return null;
		}
	}

	public Visibility VisibleIfProfile => LocalProfile != null ? Visibility.Visible : Visibility.Collapsed;
	public Visibility VisibleIfNoProfile => LocalProfile == null ? Visibility.Visible : Visibility.Collapsed;

	/// <summary>Currently in injection mode game?</summary>
	bool isRunning = false;
	public bool IsRunning {
		get => isRunning;
		set => Set(ref isRunning, value,
			[nameof(VisibleIfRunning), nameof(VisibleIfNotRunning), nameof(VisibleLateInjectWarning)]);
	}

	public Visibility VisibleIfRunning => isRunning ? Visibility.Visible : Visibility.Collapsed;
	public Visibility VisibleIfNotRunning => !isRunning ? Visibility.Visible : Visibility.Collapsed;

	public Visibility VisibleLateInjectWarning
		=> (localProfile?.Meta?.LateInjection ?? false) && !isRunning ? Visibility.Visible : Visibility.Collapsed;

	string statusMessage;
	public string StatusMessage {
		get => statusMessage; set {
			Set(ref statusMessage, value);
			if (!string.IsNullOrWhiteSpace(value)) Logger.Log.LogTrace(value);
		}
	}

	bool searchEnabled = true;
	public bool SearchEnabled { get => searchEnabled; set => Set(ref searchEnabled, value); }


	/// <summary>XR/VR</summary>
	string linkProtocol = "XR";
	public string LinkProtocol {
		get => linkProtocol;
		set => Set(ref linkProtocol, value, [nameof(LinkProtocol_VR), nameof(LinkProtocol_XR)]);
	}

	public bool LinkProtocol_XR {
		get => linkProtocol == "XR"; set {
			if (value) Set(ref linkProtocol, "XR");
		}
	}


	public bool LinkProtocol_VR {
		get => linkProtocol == "VR"; set {
			if (value) Set(ref linkProtocol, "VR");
		}
	}


	bool enableVoiceCommands;
	public bool EnableVoiceCommands { get => enableVoiceCommands; set => Set(ref enableVoiceCommands, value); }

	string defaultInputDeviceName;
	public string DefaultInputDeviceName { get => defaultInputDeviceName; set => Set(ref defaultInputDeviceName, value); }
}
