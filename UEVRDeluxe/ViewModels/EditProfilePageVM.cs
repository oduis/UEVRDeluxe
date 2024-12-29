#region Usings
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using UEVRDeluxe.Code;
#endregion

namespace UEVRDeluxe.ViewModels;

public class EditProfilePageVM : VMBase {
	GameInstallation gameInstallation;
	public GameInstallation GameInstallation { get => gameInstallation; set => Set(ref gameInstallation, value); }

	LocalProfile localProfile;
	public LocalProfile LocalProfile {
		get => localProfile;
		set => Set(ref localProfile, value, [nameof(VisibleIfProfile), nameof(VisibleIfNoProfile)]);
	}

	public Visibility VisibleIfProfile => LocalProfile != null ? Visibility.Visible : Visibility.Collapsed;
	public Visibility VisibleIfNoProfile => LocalProfile == null ? Visibility.Visible : Visibility.Collapsed;
}
