using Verse;

namespace BillIngredientSource {
	public class BillIngredientSourceSettings : ModSettings {
		public NewBillDefaultMode defaultNewBillMode = NewBillDefaultMode.AllStorages;

		public override void ExposeData() {
			Scribe_Values.Look(ref defaultNewBillMode, "defaultNewBillMode", NewBillDefaultMode.AllStorages);
		}
	}
}
