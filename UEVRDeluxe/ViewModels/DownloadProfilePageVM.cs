#region Usings
using System.Collections.ObjectModel;
using UEVRDeluxe.Common;
#endregion

namespace UEVRDeluxe.ViewModels;

public class DownloadProfilePageVM : VMBase {
	/// <summary>Since search does also returns profiles for other platforms</summary>
	public string GameEXEPath { get; set; }

	public string OriginalEXEName => System.IO.Path.GetFileNameWithoutExtension(GameEXEPath);

	ObservableCollection<ProfileMeta> profileMetas;
	public ObservableCollection<ProfileMeta> ProfileMetas { get => profileMetas; set => Set(ref profileMetas, value); }

	ProfileMeta selectedProfileMeta;
	public ProfileMeta SelectedProfileMeta { get => selectedProfileMeta; set => Set(ref selectedProfileMeta, value, [nameof(IsProfileSelected)]); }

	public bool IsProfileSelected => SelectedProfileMeta != null;
}
