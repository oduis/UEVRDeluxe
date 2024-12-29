#region Usings
using Microsoft.UI.Xaml;
using System.Collections.Generic;
using UEVRDeluxe.Code;
#endregion

namespace UEVRDeluxe.ViewModels;

public class MainPageVM : VMBase {
	List<GameInstallation> games;
	public List<GameInstallation> Games { get => games; set => Set(ref games, value); }

	public Visibility VisibleIfAdmin => !string.IsNullOrWhiteSpace(AzureManager.GetCloudAdminPasskey()) ? Visibility.Visible : Visibility.Collapsed;
}
