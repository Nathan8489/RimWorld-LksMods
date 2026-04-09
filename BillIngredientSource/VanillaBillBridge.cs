using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace BillIngredientSource {
	public static class VanillaBillBridge {
		private delegate bool TryFindBestBillIngredientsInSetDelegate(
			List<Thing> availableThings,
			Bill bill,
			List<ThingCount> chosen,
			IntVec3 rootCell,
			bool alreadySorted,
			List<IngredientCount> missingIngredients
		);

		private static readonly TryFindBestBillIngredientsInSetDelegate tryFindBestBillIngredientsInSet;

		static VanillaBillBridge() {
			var method = AccessTools.Method(
				typeof(WorkGiver_DoBill),
				"TryFindBestBillIngredientsInSet",
				new Type[] {
					typeof(List<Thing>),
					typeof(Bill),
					typeof(List<ThingCount>),
					typeof(IntVec3),
					typeof(bool),
					typeof(List<IngredientCount>)
				}
			);

			if (method == null) {
				throw new Exception("[BillIngredientSource] Failed to find WorkGiver_DoBill.TryFindBestBillIngredientsInSet");
			}

			tryFindBestBillIngredientsInSet =
				AccessTools.MethodDelegate<TryFindBestBillIngredientsInSetDelegate>(method);
		}

		public static bool TryFindBestBillIngredientsInSet(
			List<Thing> availableThings,
			Bill bill,
			List<ThingCount> chosen,
			IntVec3 rootCell,
			bool alreadySorted,
			List<IngredientCount> missingIngredients) {

			return tryFindBestBillIngredientsInSet(
				availableThings,
				bill,
				chosen,
				rootCell,
				alreadySorted,
				missingIngredients
			);
		}
	}
}