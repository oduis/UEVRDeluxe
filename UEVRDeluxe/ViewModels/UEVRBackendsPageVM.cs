#region Usings
using Microsoft.UI.Xaml;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UEVRDeluxe.Code;
#endregion

namespace UEVRDeluxe.ViewModels;

public class UEVRBackendsPageVM : VMBase {
	string praydogLatest, praydogInstalled;
	public string PraydogLatest { get => praydogLatest; set => Set(ref praydogLatest, value); }
	public string PraydogInstalled { get => praydogInstalled; set => Set(ref praydogInstalled, value); }


	string joeyHodgeLatest, joeyHodgeInstalled;
	public string JoeyHodgeLatest { get => joeyHodgeLatest; set => Set(ref joeyHodgeLatest, value); }
	public string JoeyHodgeInstalled { get => joeyHodgeInstalled; set => Set(ref joeyHodgeInstalled, value); }


	string pureDarkLatest, pureDarkInstalled;
	public string PureDarkLatest { get => pureDarkLatest; set => Set(ref pureDarkLatest, value); }
	public string PureDarkInstalled	 { get => pureDarkInstalled; set => Set(ref pureDarkInstalled, value); }

	string dortamurLatest, dortamurInstalled;
	public string DortamurLatest { get => dortamurLatest; set => Set(ref dortamurLatest, value); }
	public string DortamurInstalled { get => dortamurInstalled; set => Set(ref dortamurInstalled, value); }
}
