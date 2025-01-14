#region Usings
using Microsoft.UI.Xaml;
using System.Collections.Generic;
using System.Linq;
using UEVRDeluxe.Code;
#endregion

namespace UEVRDeluxe.ViewModels;

public class SettingsPageVM : VMBase {
	List<OpenXRRuntime> openXRRuntimes;
	public List<OpenXRRuntime> OpenXRRuntimes { get => openXRRuntimes; set => Set(ref openXRRuntimes, value, [nameof(OpenXRRuntimeVisible)]); }

	OpenXRRuntime selectedRuntime;
	public OpenXRRuntime SelectedRuntime { get => selectedRuntime; set => Set(ref selectedRuntime, value); }

	/// <summary>There are some special runtimes liek WMR und Varjo that don't play nice. Leave them alone.</summary>
	public Visibility OpenXRRuntimeVisible => (openXRRuntimes?.Count(r => r.IsDefault) ?? 0) == 1 ? Visibility.Visible : Visibility.Collapsed;
}
