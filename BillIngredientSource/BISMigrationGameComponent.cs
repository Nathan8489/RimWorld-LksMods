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

			// 이미 최신 상태
			if (legacyBillMigrationDone) {
				BillIngredientSourceMod.DebugLog("Legacy bill migration already up to date.");
				return;
			}

			int pendingCount = CountLegacyBills();

			// 마이그레이션 필요 상태 감지
			Log.Message("[BillIngredientSource] Legacy bill migration required. Pending bills: " + pendingCount);

			RunLegacyBillMigration();
			legacyBillMigrationDone = true;

			Log.Message("[BillIngredientSource] Legacy bill migration finished.");
		}

		private int CountLegacyBills() {
			int count = 0;

			if (Current.Game == null || Current.Game.Maps == null) {
				return 0;
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
							count++;
						}
					}
				}
			}

			return count;
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