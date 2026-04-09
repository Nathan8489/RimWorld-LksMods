using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace BillIngredientSource {
	public static class VanillaBillBridge {
		[HarmonyReversePatch]
		[HarmonyPatch(typeof(WorkGiver_DoBill), "TryFindBestBillIngredientsInSet")]
		public static bool TryFindBestBillIngredientsInSet(
			List<Thing> availableThings,
			Bill bill,
			List<ThingCount> chosen,
			IntVec3 rootCell,
			bool alreadySorted,
			List<IngredientCount> missingIngredients) {
			throw new System.NotImplementedException("Harmony reverse patch stub");
		}
	}
}