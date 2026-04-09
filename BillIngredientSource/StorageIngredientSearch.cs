using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace BillIngredientSource {
	public static class StorageIngredientSearch {
		private sealed class StorageUnit {
			public List<SlotGroup> SlotGroups = new List<SlotGroup>();
			public int MinDistSqToRoot;
		}

		public static bool TryFindBestBillIngredientsFromStorageUsingVanillaSelector(
			Bill_Production bill,
			Pawn pawn,
			Thing billGiver,
			List<ThingCount> chosen,
			List<IngredientCount> missingIngredients) {

			chosen.Clear();
			missingIngredients?.Clear();

			if (bill == null || pawn == null || billGiver == null || billGiver.Map == null) {
				return false;
			}

			Map map = billGiver.Map;
			IntVec3 rootCell = GetBillGiverRootCell(billGiver, pawn);
			TraverseParms traverseParms = TraverseParms.For(pawn, Danger.Deadly);

			if (IsAllStoragesMode(bill)) {
				return TryFindFromAllStoragesIncremental(
					bill,
					pawn,
					map,
					rootCell,
					traverseParms,
					chosen,
					missingIngredients
				);
			}

			List<Thing> candidates = StorageIngredientSource.GetCandidateThings(map, bill);
			List<Thing> available = new List<Thing>(candidates.Count);

			for (int i = 0; i < candidates.Count; i++) {
				TryAddThingToAvailable(
					candidates[i],
					bill,
					pawn,
					map,
					rootCell,
					traverseParms,
					available
				);
			}

			Log.Message($"[BIS] storage candidates={candidates.Count}, available={available.Count}");

			bool result = RunVanillaSelector(
				available,
				bill,
				rootCell,
				chosen,
				missingIngredients
			);

			Log.Message($"[BIS] vanilla selector result={result}, chosen={chosen.Count}, missing={(missingIngredients?.Count ?? -1)}");
			return result;
		}

		private static bool TryFindFromAllStoragesIncremental(
			Bill_Production bill,
			Pawn pawn,
			Map map,
			IntVec3 rootCell,
			TraverseParms traverseParms,
			List<ThingCount> chosen,
			List<IngredientCount> missingIngredients) {

			List<StorageUnit> units = BuildAllStorageUnits(map, rootCell);
			List<Thing> available = new List<Thing>();
			HashSet<int> seenThingIds = new HashSet<int>();

			Log.Message($"[BIS] all storages units={units.Count}");

			bool attempted = false;

			for (int i = 0; i < units.Count; i++) {
				int beforeCount = available.Count;
				AddUnitThingsToAvailable(
					units[i],
					bill,
					pawn,
					map,
					rootCell,
					traverseParms,
					seenThingIds,
					available
				);

				// 새 후보가 하나도 안 늘었으면 selector를 다시 돌릴 필요 없음
				if (available.Count == beforeCount) {
					continue;
				}

				attempted = true;

				bool result = RunVanillaSelector(
					available,
					bill,
					rootCell,
					chosen,
					missingIngredients
				);

				Log.Message($"[BIS] all storages step={i + 1}/{units.Count}, available={available.Count}, result={result}, chosen={chosen.Count}, missing={(missingIngredients?.Count ?? -1)}");

				if (result) {
					return true;
				}
			}

			// 저장소는 있었지만 usable 후보가 하나도 없었던 경우에도
			// missingIngredients를 채우기 위해 selector를 한 번은 돌린다.
			if (!attempted) {
				bool result = RunVanillaSelector(
					available,
					bill,
					rootCell,
					chosen,
					missingIngredients
				);

				Log.Message($"[BIS] all storages final-empty available={available.Count}, result={result}, chosen={chosen.Count}, missing={(missingIngredients?.Count ?? -1)}");
				return result;
			}

			return false;
		}

		private static bool RunVanillaSelector(
			List<Thing> available,
			Bill_Production bill,
			IntVec3 rootCell,
			List<ThingCount> chosen,
			List<IngredientCount> missingIngredients) {

			return VanillaBillBridge.TryFindBestBillIngredientsInSet(
				available,
				bill,
				chosen,
				rootCell,
				false,
				missingIngredients
			);
		}

		private static bool IsAllStoragesMode(Bill_Production bill) {
			if (bill == null) {
				return false;
			}

			if (!BillDataStore.TryGet(bill, out BillData data) || data == null) {
				return false;
			}

			return data.SelectedStorageId == BISIds.AllStorages;
		}

		private static List<StorageUnit> BuildAllStorageUnits(Map map, IntVec3 rootCell) {
			List<StorageUnit> result = new List<StorageUnit>();

			if (map == null) {
				return result;
			}

			// 1) Zone은 별도 단위
			List<Zone> allZones = map.zoneManager.AllZones;
			for (int i = 0; i < allZones.Count; i++) {
				Zone_Stockpile zone = allZones[i] as Zone_Stockpile;
				if (zone == null) {
					continue;
				}

				SlotGroup zoneGroup = zone.GetSlotGroup();
				if (zoneGroup == null) {
					continue;
				}

				StorageUnit unit = new StorageUnit();
				unit.SlotGroups.Add(zoneGroup);
				unit.MinDistSqToRoot = GetMinDistSq(rootCell, zoneGroup.CellsList);
				result.Add(unit);
			}

			// 2) 일반 storage / storage group
			HashSet<StorageGroup> addedStorageGroups = new HashSet<StorageGroup>();
			List<SlotGroup> allGroups = map.haulDestinationManager.AllGroupsListInPriorityOrder;

			for (int i = 0; i < allGroups.Count; i++) {
				SlotGroup sg = allGroups[i];
				if (sg == null) {
					continue;
				}

				// Zone은 위에서 이미 처리
				if (sg.parent is Zone_Stockpile) {
					continue;
				}

				if (sg.StorageGroup != null) {
					if (!addedStorageGroups.Add(sg.StorageGroup)) {
						continue;
					}

					StorageUnit unit = new StorageUnit();
					int minDistSq = int.MaxValue;

					for (int j = 0; j < allGroups.Count; j++) {
						SlotGroup member = allGroups[j];
						if (member == null || member.StorageGroup != sg.StorageGroup) {
							continue;
						}

						unit.SlotGroups.Add(member);

						int distSq = GetMinDistSq(rootCell, member.CellsList);
						if (distSq < minDistSq) {
							minDistSq = distSq;
						}
					}

					unit.MinDistSqToRoot = minDistSq;
					result.Add(unit);
					continue;
				}

				StorageUnit singleUnit = new StorageUnit();
				singleUnit.SlotGroups.Add(sg);
				singleUnit.MinDistSqToRoot = GetMinDistSq(rootCell, sg.CellsList);
				result.Add(singleUnit);
			}

			result.Sort((a, b) => a.MinDistSqToRoot.CompareTo(b.MinDistSqToRoot));
			return result;
		}

		private static void AddUnitThingsToAvailable(
			StorageUnit unit,
			Bill_Production bill,
			Pawn pawn,
			Map map,
			IntVec3 rootCell,
			TraverseParms traverseParms,
			HashSet<int> seenThingIds,
			List<Thing> available) {

			if (unit == null) {
				return;
			}

			for (int i = 0; i < unit.SlotGroups.Count; i++) {
				SlotGroup slotGroup = unit.SlotGroups[i];
				if (slotGroup == null) {
					continue;
				}

				foreach (Thing thing in slotGroup.HeldThings) {
					if (thing == null) {
						continue;
					}

					if (!seenThingIds.Add(thing.thingIDNumber)) {
						continue;
					}

					TryAddThingToAvailable(
						thing,
						bill,
						pawn,
						map,
						rootCell,
						traverseParms,
						available
					);
				}
			}
		}

		private static void TryAddThingToAvailable(
			Thing t,
			Bill_Production bill,
			Pawn pawn,
			Map map,
			IntVec3 rootCell,
			TraverseParms traverseParms,
			List<Thing> available) {

			if (t == null || !t.Spawned || t.Map != map) {
				return;
			}

			if (t.IsForbidden(pawn)) {
				return;
			}

			if (!pawn.CanReserve(t)) {
				return;
			}

			if (!IsUsableIngredientForBill(t, bill)) {
				return;
			}

			// 작업대(rootCell) 기준 접근성 판정
			if (!map.reachability.CanReach(rootCell, t, PathEndMode.ClosestTouch, traverseParms)) {
				return;
			}

			available.Add(t);
		}

		private static int GetMinDistSq(IntVec3 rootCell, List<IntVec3> cells) {
			if (cells == null || cells.Count == 0) {
				return int.MaxValue;
			}

			int min = int.MaxValue;
			for (int i = 0; i < cells.Count; i++) {
				int dist = (cells[i] - rootCell).LengthHorizontalSquared;
				if (dist < min) {
					min = dist;
				}
			}

			return min;
		}

		private static bool IsUsableIngredientForBill(Thing t, Bill bill) {
			if (!bill.IsFixedOrAllowedIngredient(t)) {
				return false;
			}

			List<IngredientCount> ingredients = bill.recipe.ingredients;
			for (int i = 0; i < ingredients.Count; i++) {
				if (ingredients[i].filter.Allows(t)) {
					return true;
				}
			}

			return false;
		}

		private static IntVec3 GetBillGiverRootCell(Thing billGiver, Pawn forPawn) {
			if (billGiver is Building building) {
				if (building.def.hasInteractionCell) {
					return building.InteractionCell;
				}

				return forPawn.Position;
			}

			return billGiver.Position;
		}
	}
}