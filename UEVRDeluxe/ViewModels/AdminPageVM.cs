#region Usings
using System.Collections.ObjectModel;
using UEVRDeluxe.Common;
#endregion

namespace UEVRDeluxe.ViewModels;

public class AdminPageVM : VMBase {
	string searchEXEName;
	public string SearchEXEName { get => searchEXEName; set => Set(ref searchEXEName, value); }

	ObservableCollection<ProfileMeta> profileMetas;
	public ObservableCollection<ProfileMeta> ProfileMetas { get => profileMetas; set => Set(ref profileMetas, value); }

	ProfileMeta selectedProfileMeta;
	public ProfileMeta SelectedProfileMeta { get => selectedProfileMeta; set => Set(ref selectedProfileMeta, value, [nameof(IsProfileSelected)]); }

	public bool IsProfileSelected => SelectedProfileMeta != null;
}

