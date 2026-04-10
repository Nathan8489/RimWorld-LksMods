using RimWorld;
using Verse;

namespace BillIngredientSource {
	public class BISMigrationGameComponent : GameComponent {
		private bool legacyBillMigrationDone;

		public BISMigrationGameComponent(Game game) {
		}

		public override void ExposeData() {
			base.ExposeData();
			Scribe_Values.Look(ref legacyBillMigrationDone, "legacyBillMigrationDone", false);
		}

		public override void FinalizeInit() {
			base.FinalizeInit();

			if (legacyBillMigrationDone) {
				return;
			}

			RunLegacyBillMigration();
			legacyBillMigrationDone = true;

			if (Prefs.DevMode) {
				Log.Message("[BillIngredientSource] Legacy bill migration finished.");
			}
		}

		private void RunLegacyBillMigration() {
			if (Current.Game == null || Current.Game.Maps == null) {
				return;
			}

			for (int m = 0; m < Current.Game.Maps.Count; m++) {
				Map map = Current.Game.Maps[m];
				if (map == null) {
					continue;
				}

				var allThings = map.listerThings.AllThings;
				for (int i = 0; i < allThings.Count; i++) {
					Thing thing = allThings[i];
					IBillGiver billGiver = thing as IBillGiver;
					if (billGiver == null) {
						continue;
					}

					BillStack stack = billGiver.BillStack;
					if (stack == null) {
						continue;
					}

					for (int j = 0; j < stack.Count; j++) {
						Bill_Production bill = stack[j] as Bill_Production;
						if (bill == null) {
							continue;
						}

						BillData data = BillDataStore.GetOrCreate(bill);
						if (data != null && data.HasLegacyData()) {
							LegacyStorageMigration.TryMigrate(bill, data);
						}
					}
				}
			}
		}
	}
}