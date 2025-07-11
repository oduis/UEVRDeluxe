using System.Collections.Generic;
using UEVRDeluxe.Common;

namespace UEVRDeluxe.ViewModels;

public class AllProfilesPageVM : VMBase {
	List<ProfileMeta> allProfileMetas;
	public List<ProfileMeta> AllProfileMetas {
		get => allProfileMetas;
		set => Set(ref allProfileMetas, value);
	}
}
