using HarmonyLib;
using RimWorld;
using Verse;

namespace BillIngredientSource {
	[HarmonyPatch(typeof(BillStack), "AddBill")]
	public static class Patch_BillStack_AddBill {
		public static void Postfix(BillStack __instance, Bill bill) {
			if (Scribe.mode != LoadSaveMode.Inactive) {
				return;
			}

			Bill_Production productionBill = bill as Bill_Production;
			if (productionBill == null) {
				return;
			}

			BillData data = BillDataStore.GetOrCreate(productionBill);

			// 이미 설정된 Bill은 건드리지 않음
			if (!string.IsNullOrEmpty(data.SelectedStorageId) || data.SelectedZoneId != -1 || !string.IsNullOrEmpty(data.SelectedZoneLabel)) {
				return;
			}

			NewBillDefaultMode defaultMode = BillIngredientSourceMod.Settings?.defaultNewBillMode ?? NewBillDefaultMode.AllStorages;
			switch (defaultMode) {
				case NewBillDefaultMode.AllStorages:
					data.SelectedStorageId = BISIds.AllStorages;
					data.SelectedZoneId = -1;
					data.SelectedZoneLabel = "BIS_AllStorages".Translate().ToString();
					data.SearchMode = IngredientSearchMode.Storage;
					break;

				default:
					data.SelectedStorageId = null;
					data.SelectedZoneId = -1;
					data.SelectedZoneLabel = null;
					data.SearchMode = IngredientSearchMode.Radius;
					break;
			}

			BillDataStore.Set(productionBill, data);
		}
	}
}
