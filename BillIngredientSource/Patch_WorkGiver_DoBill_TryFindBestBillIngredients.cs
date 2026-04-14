using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace BillIngredientSource {
	[HarmonyPatch(typeof(WorkGiver_DoBill), "TryFindBestBillIngredients")]
	public static class Patch_WorkGiver_DoBill_TryFindBestBillIngredients {
		public static bool Prefix(
			Bill bill,
			Pawn pawn,
			Thing billGiver,
			List<ThingCount> chosen,
			List<IngredientCount> missingIngredients,
			ref bool __result) {

			Bill_Production productionBill = bill as Bill_Production;
			if (productionBill == null)
				return true;

			BillData data;
			if (!BillDataStore.TryGet(productionBill, out data) || data == null)
				return true;

			if (!data.UseAllStorages && data.SelectedStorageGroup == null && data.HasLegacyData()) {
				LegacyStorageMigration.TryMigrate(productionBill, data);
			}

			bool useStorage = data.UseAllStorages || data.SelectedStorageGroup != null;
			if (!useStorage)
				return true;

			__result = StorageIngredientSearch.TryFindBestBillIngredientsFromStorageUsingVanillaSelector(
				productionBill,
				pawn,
				billGiver,
				chosen,
				missingIngredients
			);

			return false;
		}
	}
}