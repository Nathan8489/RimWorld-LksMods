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
			if (!(bill is Bill_Production productionBill))
				return true;

			if (!BillDataStore.TryGet(productionBill, out var data))
				return true;

			if (string.IsNullOrEmpty(data.SelectedStorageId))
				return true; // 바닐라 그대로

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