#region Usings
using Microsoft.UI.Xaml;
using UEVRDeluxe.Code;
#endregion

namespace UEVRDeluxe.ViewModels;

public class EditProfilePageVM : VMBase {
	GameInstallation gameInstallation;
	public GameInstallation GameInstallation { get => gameInstallation; set => Set(ref gameInstallation, value); }

	LocalProfile localProfile;
	public LocalProfile LocalProfile {
		get => localProfile;
		set {
			VisibleIfProfile = (value?.GetExists() ?? false) ? Visibility.Visible : Visibility.Collapsed;
			Set(ref localProfile, value, [nameof(VisibleIfProfile)]);

			DescriptionMD = string.IsNullOrWhiteSpace(value.DescriptionMD)
				? LocalProfile.DUMMY_DESCRIPTION_MD : value.DescriptionMD;
		}
	}

	string descriptionMD;
	/// <summary>Might contain a dummy description that should not be changed or saved</summary>
	public string DescriptionMD { get => descriptionMD; set => Set(ref descriptionMD, value); }

	public Visibility VisibleIfProfile;
}
