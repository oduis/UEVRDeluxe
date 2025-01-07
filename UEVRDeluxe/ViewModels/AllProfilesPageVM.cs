using System.Collections.Generic;

namespace UEVRDeluxe.ViewModels;

public class AllProfilesPageVM : VMBase {
	string allProfileNames;
	public string AllProfileNames {
		get => allProfileNames;
		set => Set(ref allProfileNames, value);
	}
}
