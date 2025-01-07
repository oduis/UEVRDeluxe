#region Usings
using Microsoft.UI.Xaml;
using System.Collections.Generic;
using System.Linq;
using UEVRDeluxe.Code;
#endregion

namespace UEVRDeluxe.ViewModels;

public class MainPageVM : VMBase {
	List<GameInstallation> games;
	public List<GameInstallation> Games { get => games; set => Set(ref games, value); }

	bool findingGames;
	public bool FindingGames { get => findingGames; set => Set(ref findingGames, value); }

	string warning;
	public string Warning { get => warning; set => Set(ref warning, value, [nameof(WarningVisible)]); }
	public Visibility WarningVisible => string.IsNullOrWhiteSpace(warning) ? Visibility.Collapsed : Visibility.Visible;

	List<OpenXRRuntime> openXRRuntimes;
	public List<OpenXRRuntime> OpenXRRuntimes { get => openXRRuntimes; set => Set(ref openXRRuntimes, value, [nameof(OpenXRRuntimeVisible)]); }

	OpenXRRuntime selectedRuntime;
	public OpenXRRuntime SelectedRuntime { get => selectedRuntime; set => Set(ref selectedRuntime, value); }

	/// <summary>There are some special runtimes liek WMR und Varjo that don't play nice. Leave them alone.</summary>
	public Visibility OpenXRRuntimeVisible => (openXRRuntimes?.Count(r => r.IsDefault) ?? 0) == 1 ? Visibility.Visible : Visibility.Collapsed;

	public Visibility VisibleIfAdmin => !string.IsNullOrWhiteSpace(AzureManager.GetCloudAdminPasskey()) ? Visibility.Visible : Visibility.Collapsed;
}
