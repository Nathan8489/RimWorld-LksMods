using System;
using System.Runtime.CompilerServices;
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

		private struct StorageUnitsCacheKey : IEquatable<StorageUnitsCacheKey> {
			public int MapId;
			public IntVec3 RootCell;

			public bool Equals(StorageUnitsCacheKey other) {
				return MapId == other.MapId && RootCell == other.RootCell;
			}

			public override bool Equals(object obj) {
				return obj is StorageUnitsCacheKey other && Equals(other);
			}

			public override int GetHashCode() {
				unchecked {
					int hash = MapId;
					hash = (hash * 397) ^ RootCell.GetHashCode();
					return hash;
				}
			}
		}

		private sealed class StorageUnitsCacheEntry {
			public long TopologySignature;
			public List<StorageUnit> Units;
			public int LastAccessTick;
		}

		private static readonly Dictionary<StorageUnitsCacheKey, StorageUnitsCacheEntry> allStoragesCache
			= new Dictionary<StorageUnitsCacheKey, StorageUnitsCacheEntry>();

		private const int MaxAllStoragesCacheEntries = 128;

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

#if DEBUG
			Log.Message($"[BIS] storage candidates={candidates.Count}, available={available.Count}");
#endif

			bool result = RunVanillaSelector(
				available,
				bill,
				rootCell,
				chosen,
				missingIngredients
			);

			// selector 결과는 유저 디버깅에도 쓸 수 있음 → DevMode 유지
#if DEBUG
			if (Prefs.DevMode)
				Log.Message($"[BIS] selector result={result}, chosen={chosen.Count}, missing={(missingIngredients?.Count ?? -1)}");
#endif
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

			List<StorageUnit> units = GetAllStorageUnitsCached(map, rootCell);
			List<Thing> available = new List<Thing>();
			HashSet<int> seenThingIds = new HashSet<int>();

#if DEBUG
			Log.Message($"[BIS] all storages units={units.Count}");
#endif

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

#if DEBUG
				Log.Message($"[BIS] all storages step={i + 1}/{units.Count}, available={available.Count}, result={result}, chosen={chosen.Count}, missing={(missingIngredients?.Count ?? -1)}");
#endif

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

#if DEBUG
				Log.Message($"[BIS] all storages final-empty available={available.Count}, result={result}, chosen={chosen.Count}, missing={(missingIngredients?.Count ?? -1)}");
#endif
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

			return data.UseAllStorages;
		}

		private static List<StorageUnit> BuildAllStorageUnitsUncached(Map map, IntVec3 rootCell) {
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
#if DEBUG
			if (Prefs.DevMode) {
				Log.Message("[BIS] Sorted storage units for rootCell " + rootCell + ":");
				for (int i = 0; i < result.Count; i++) {
					Log.Message("[BIS]   " + i + ": " + GetStorageUnitDebugLabel(result[i]) + " distSq=" + result[i].MinDistSqToRoot);
				}
			}
#endif
			return result;
		}

		private static string GetStorageUnitDebugLabel(StorageUnit unit) {
			if (unit == null || unit.SlotGroups == null || unit.SlotGroups.Count == 0) {
				return "<empty>";
			}

			SlotGroup sg = unit.SlotGroups[0];
			if (sg == null) {
				return "<null>";
			}

			if (sg.parent is Zone_Stockpile zone) {
				return "[Zone] " + zone.label;
			}

			if (sg.StorageGroup != null) {
				return "[StorageGroup] " + SlotGroup.GetGroupLabel(sg.StorageGroup);
			}

			return "[Storage] " + SlotGroup.GetGroupLabel(sg);
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

		private static List<StorageUnit> GetAllStorageUnitsCached(Map map, IntVec3 rootCell) {
			if (map == null) {
				return new List<StorageUnit>();
			}

			StorageUnitsCacheKey key = new StorageUnitsCacheKey {
				MapId = map.uniqueID,
				RootCell = rootCell
			};

			long signature = ComputeAllStoragesTopologySignature(map);

			StorageUnitsCacheEntry entry;
			if (allStoragesCache.TryGetValue(key, out entry) && entry != null && entry.Units != null) {
				if (entry.TopologySignature == signature) {
					entry.LastAccessTick = Find.TickManager != null ? Find.TickManager.TicksGame : 0;

#if DEBUG
					if (Prefs.DevMode) {
						Log.Message("[BIS] AllStorages cache HIT map=" + map.uniqueID + " root=" + rootCell);
					}
#endif
					return entry.Units;
				}

#if DEBUG
				if (Prefs.DevMode) {
					Log.Message("[BIS] AllStorages cache REBUILD map=" + map.uniqueID + " root=" + rootCell);
				}
#endif
			} else {
#if DEBUG
				if (Prefs.DevMode) {
					Log.Message("[BIS] AllStorages cache MISS map=" + map.uniqueID + " root=" + rootCell);
				}
#endif
			}

			List<StorageUnit> units = BuildAllStorageUnitsUncached(map, rootCell);

			allStoragesCache[key] = new StorageUnitsCacheEntry {
				TopologySignature = signature,
				Units = units,
				LastAccessTick = Find.TickManager != null ? Find.TickManager.TicksGame : 0
			};

			TrimAllStoragesCacheIfNeeded();
			return units;
		}

		private static long ComputeAllStoragesTopologySignature(Map map) {
			if (map == null) {
				return 0L;
			}

			unchecked {
				long hash = 1469598103934665603L;

				// Zone_Stockpile
				List<Zone> allZones = map.zoneManager.AllZones;
				for (int i = 0; i < allZones.Count; i++) {
					Zone_Stockpile zone = allZones[i] as Zone_Stockpile;
					if (zone == null) {
						continue;
					}

					hash = Mix(hash, zone.ID);
					hash = Mix(hash, zone.cells != null ? zone.cells.Count : 0);
				}

				// 일반 storage / storage group
				List<SlotGroup> allGroups = map.haulDestinationManager.AllGroupsListInPriorityOrder;
				HashSet<StorageGroup> seenStorageGroups = new HashSet<StorageGroup>();

				for (int i = 0; i < allGroups.Count; i++) {
					SlotGroup sg = allGroups[i];
					if (sg == null) {
						continue;
					}

					// Zone은 zone 루프에서만 반영
					if (sg.parent is Zone_Stockpile) {
						continue;
					}

					if (sg.StorageGroup != null) {
						if (!seenStorageGroups.Add(sg.StorageGroup)) {
							continue;
						}

						hash = Mix(hash, RuntimeHelpers.GetHashCode(sg.StorageGroup));

						int memberCount = 0;
						int totalCellCount = 0;

						for (int j = 0; j < allGroups.Count; j++) {
							SlotGroup member = allGroups[j];
							if (member == null || member.StorageGroup != sg.StorageGroup) {
								continue;
							}

							memberCount++;
							totalCellCount += member.CellsList != null ? member.CellsList.Count : 0;
						}

						hash = Mix(hash, memberCount);
						hash = Mix(hash, totalCellCount);
					} else {
						hash = Mix(hash, RuntimeHelpers.GetHashCode(sg));
						hash = Mix(hash, sg.CellsList != null ? sg.CellsList.Count : 0);
					}
				}

				return hash;
			}
		}

		private static long Mix(long hash, int value) {
			unchecked {
				hash ^= value;
				hash *= 1099511628211L;
				return hash;
			}
		}

		private static void TrimAllStoragesCacheIfNeeded() {
			if (allStoragesCache.Count <= MaxAllStoragesCacheEntries) {
				return;
			}

			StorageUnitsCacheKey oldestKey = default(StorageUnitsCacheKey);
			int oldestTick = int.MaxValue;
			bool found = false;

			foreach (KeyValuePair<StorageUnitsCacheKey, StorageUnitsCacheEntry> kvp in allStoragesCache) {
				if (kvp.Value == null) {
					oldestKey = kvp.Key;
					found = true;
					break;
				}

				if (!found || kvp.Value.LastAccessTick < oldestTick) {
					oldestTick = kvp.Value.LastAccessTick;
					oldestKey = kvp.Key;
					found = true;
				}
			}

			if (found) {
				allStoragesCache.Remove(oldestKey);
			}
		}

		public static void ClearAllStoragesCache() {
			allStoragesCache.Clear();
		}
	}
}