#region Usings
using Microsoft.UI.Xaml;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UEVRDeluxe.Code;
#endregion

namespace UEVRDeluxe.ViewModels;

public class MainPageVM : VMBase {
	ObservableCollection<GameInstallation> games;
	public ObservableCollection<GameInstallation> Games { get => games; set => Set(ref games, value); }

	string warning;
	public string Warning { get => warning; set => Set(ref warning, value, [nameof(WarningVisible)]); }
	public Visibility WarningVisible => string.IsNullOrWhiteSpace(warning) ? Visibility.Collapsed : Visibility.Visible;


	Visibility pleaseWaitVisible = Visibility.Collapsed;
	public Visibility PleaseWaitVisible { get => pleaseWaitVisible; set => Set(ref pleaseWaitVisible, value); }

	public Visibility VisibleIfAdmin => !string.IsNullOrWhiteSpace(AzureManager.GetCloudAdminPasskey()) ? Visibility.Visible : Visibility.Collapsed;

	public const string UEVR_BACKEND_LABEL = "Update UEVR Backends";
	string uevrBackendsLabel = UEVR_BACKEND_LABEL;
	public string UEVRBackendsLabel { get => uevrBackendsLabel; set => Set(ref uevrBackendsLabel, value); }
}
