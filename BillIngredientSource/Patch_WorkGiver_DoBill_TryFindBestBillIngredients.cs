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
			ref IngredientCount missingIngredient,
			ref bool __result) {

			Bill_Production productionBill = bill as Bill_Production;
			if (productionBill == null) {
				return true;
			}

			BillData data;
			if (!BillDataStore.TryGet(productionBill, out data)) {
				return true;
			}

			if (data.SearchMode != IngredientSearchMode.Storage) {
				return true;
			}

			__result = StorageIngredientSearch.TryFindBestBillIngredientsFromStorage(
				productionBill,
				pawn,
				billGiver,
				chosen,
				out missingIngredient
			);

			return false;
		}
	}
}