using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

namespace BillIngredientSource {
	public static class StorageIngredientSearch {
		public static bool TryFindBestBillIngredientsFromStorage(
			Bill_Production bill,
			Pawn pawn,
			Thing billGiver,
			List<ThingCount> chosen,
			out IngredientCount missingIngredient) {

			missingIngredient = null;
			chosen.Clear();

			if (bill == null || pawn == null || billGiver == null || billGiver.Map == null) {
				return false;
			}

			List<Thing> candidates = StorageIngredientSource.GetCandidateThings(billGiver.Map, bill);

			// 최소한의 기본 필터
			List<Thing> usable = new List<Thing>();
			for (int i = 0; i < candidates.Count; i++) {
				Thing thing = candidates[i];
				if (thing == null || !thing.Spawned || thing.Map != billGiver.Map) {
					continue;
				}
				if (thing.IsForbidden(pawn)) {
					continue;
				}
				if (!pawn.CanReserve(thing)) {
					continue;
				}
				if (!pawn.CanReach(thing, PathEndMode.ClosestTouch, Danger.Deadly)) {
					continue;
				}

				usable.Add(thing);
			}

			// 가까운 것부터 사용
			usable.SortBy(t => (t.Position - billGiver.Position).LengthHorizontalSquared);

			Dictionary<Thing, int> remaining = new Dictionary<Thing, int>();
			for (int i = 0; i < usable.Count; i++) {
				remaining[usable[i]] = usable[i].stackCount;
			}

			List<IngredientCount> ingredients = bill.recipe.ingredients;
			for (int i = 0; i < ingredients.Count; i++) {
				IngredientCount ingredient = ingredients[i];
				float need = ingredient.GetBaseCount();

				for (int j = 0; j < usable.Count; j++) {
					Thing thing = usable[j];

					int remain;
					if (!remaining.TryGetValue(thing, out remain) || remain <= 0) {
						continue;
					}

					if (!ingredient.filter.Allows(thing)) {
						continue;
					}

					int take = Mathf.Min(remain, Mathf.CeilToInt(need));
					if (take <= 0) {
						continue;
					}

					chosen.Add(new ThingCount(thing, take));
					remaining[thing] = remain - take;
					need -= take;

					if (need <= 0.001f) {
						break;
					}
				}

				if (need > 0.001f) {
					missingIngredient = ingredient;
					chosen.Clear();
					return false;
				}
			}

			return chosen.Count > 0;
		}
	}
}