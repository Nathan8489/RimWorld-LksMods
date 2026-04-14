using Verse;

namespace BillIngredientSource {
	public class BillIngredientSourceSettings : ModSettings {
		public NewBillDefaultMode defaultNewBillMode = NewBillDefaultMode.AllStorages;
		public bool enableDebugLogging = false;

		public override void ExposeData() {
			Scribe_Values.Look(ref defaultNewBillMode, "defaultNewBillMode", NewBillDefaultMode.AllStorages);
			Scribe_Values.Look(ref enableDebugLogging, "enableDebugLogging", false);
		}
	}
}