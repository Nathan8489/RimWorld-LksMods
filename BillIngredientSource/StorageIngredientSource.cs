using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace BillIngredientSource {
	public static class StorageIngredientSource {
		public static List<Thing> GetCandidateThings(Map map, Bill_Production bill) {
			List<Thing> result = new List<Thing>();

			if (map == null || bill == null) {
				return result;
			}

			if (!BillDataStore.TryGet(bill, out BillData data)) {
				return result;
			}

			if (string.IsNullOrEmpty(data.SelectedStorageId)) {
				return result;
			}

			HashSet<int> seenThingIds = new HashSet<int>();

			if (data.SelectedStorageId == BISIds.AllStorages) {
				AddAllStorageThings(map, result, seenThingIds);
				return result;
			}

			if (data.SelectedStorageId.StartsWith(BISIds.ZonePrefix)) {
				if (TryParseTailInt(data.SelectedStorageId, BISIds.ZonePrefix, out int zoneId)) {
					Zone_Stockpile zone = FindZoneById(map, zoneId);
					if (zone != null) {
						AddSlotGroupThings(zone.GetSlotGroup(), result, seenThingIds);
					}
				}
				return result;
			}

			if (data.SelectedStorageId.StartsWith(BISIds.StorageGroupCellPrefix)) {
				ISlotGroup group = FindStorageGroupByCellId(map, data.SelectedStorageId);
				if (group != null) {
					AddISlotGroupThings(map, group, result, seenThingIds);
				}
				return result;
			}

			if (data.SelectedStorageId.StartsWith(BISIds.SlotGroupPrefix)) {
				SlotGroup slotGroup = FindSlotGroupByStorageId(map, data.SelectedStorageId);
				if (slotGroup != null) {
					AddSlotGroupThings(slotGroup, result, seenThingIds);
				}
				return result;
			}

			return result;
		}

		private static ISlotGroup FindStorageGroupByCellId(Map map, string storageId) {
			if (map == null || string.IsNullOrEmpty(storageId) || !storageId.StartsWith(BISIds.StorageGroupCellPrefix)) {
				return null;
			}

			string[] parts = storageId.Split('_');
			if (parts.Length != 3) {
				return null;
			}

			if (!int.TryParse(parts[1], out int x)) return null;
			if (!int.TryParse(parts[2], out int z)) return null;

			IntVec3 target = new IntVec3(x, 0, z);

			List<SlotGroup> allGroups = map.haulDestinationManager.AllGroupsListInPriorityOrder;
			for (int i = 0; i < allGroups.Count; i++) {
				SlotGroup sg = allGroups[i];
				if (sg.CellsList.Contains(target)) {
					return sg.StorageGroup ?? (ISlotGroup)sg;
				}
			}

			return null;
		}

		private static void AddAllStorageThings(Map map, List<Thing> result, HashSet<int> seenThingIds) {
			List<Zone> allZones = map.zoneManager.AllZones;
			for (int i = 0; i < allZones.Count; i++) {
				Zone_Stockpile zone = allZones[i] as Zone_Stockpile;
				if (zone != null) {
					AddSlotGroupThings(zone.GetSlotGroup(), result, seenThingIds);
				}
			}

			List<SlotGroup> allGroups = map.haulDestinationManager.AllGroupsListInPriorityOrder;
			for (int i = 0; i < allGroups.Count; i++) {
				AddSlotGroupThings(allGroups[i], result, seenThingIds);
			}
		}

		private static void AddISlotGroupThings(Map map, ISlotGroup group, List<Thing> result, HashSet<int> seenThingIds) {
			if (group == null) {
				return;
			}

			if (group is SlotGroup slotGroup) {
				AddSlotGroupThings(slotGroup, result, seenThingIds);
				return;
			}

			// StorageGroup인 경우, 그 그룹에 속한 모든 SlotGroup을 합침
			List<SlotGroup> allGroups = map.haulDestinationManager.AllGroupsListInPriorityOrder;
			for (int i = 0; i < allGroups.Count; i++) {
				SlotGroup sg = allGroups[i];
				if (sg.StorageGroup == group) {
					AddSlotGroupThings(sg, result, seenThingIds);
				}
			}
		}

		private static void AddSlotGroupThings(SlotGroup slotGroup, List<Thing> result, HashSet<int> seenThingIds) {
			if (slotGroup == null) {
				return;
			}

			foreach (Thing thing in slotGroup.HeldThings) {
				if (thing == null) {
					continue;
				}

				if (!seenThingIds.Add(thing.thingIDNumber)) {
					continue;
				}

				result.Add(thing);
			}
		}

		public static string GetStorageLabel(Map map, string storageId, string fallbackLabel = null) {
			if (string.IsNullOrEmpty(storageId)) {
				return fallbackLabel;
			}

			if (storageId == BISIds.AllStorages) {
				return "BIS_AllStorages".Translate().ToString();
			}

			if (map != null && storageId.StartsWith(BISIds.ZonePrefix)) {
				if (TryParseTailInt(storageId, BISIds.ZonePrefix, out int zoneId)) {
					Zone_Stockpile zone = FindZoneById(map, zoneId);
					if (zone != null) {
						return zone.label;
					}
				}
				return "BIS_MissingZone".Translate().ToString();
			}

			if (map != null && storageId.StartsWith(BISIds.StorageGroupCellPrefix)) {
				ISlotGroup group = FindStorageGroupByCellId(map, storageId);
				if (group != null) {
					return SlotGroup.GetGroupLabel(group);
				}
				return "BIS_MissingStorage".Translate().ToString();
			}

			if (map != null && storageId.StartsWith(BISIds.SlotGroupPrefix)) {
				SlotGroup slotGroup = FindSlotGroupByStorageId(map, storageId);
				if (slotGroup != null) {
					return SlotGroup.GetGroupLabel(slotGroup);
				}
				return "BIS_MissingStorage".Translate().ToString();
			}

			return fallbackLabel;
		}

		public static IEnumerable<ISlotGroup> GetAllSelectableStorageGroups(Map map) {
			if (map == null) {
				return Enumerable.Empty<ISlotGroup>();
			}

			List<SlotGroup> allGroups = map.haulDestinationManager.AllGroupsListInPriorityOrder;
			Dictionary<string, List<ISlotGroup>> tmpGroups = new Dictionary<string, List<ISlotGroup>>();

			for (int i = 0; i < allGroups.Count; i++) {
				SlotGroup slotGroup = allGroups[i];

				if (slotGroup.StorageGroup != null) {
					StorageGroup storageGroup = slotGroup.StorageGroup;
					if (!tmpGroups.ContainsKey(storageGroup.GroupingLabel)) {
						tmpGroups.Add(storageGroup.GroupingLabel, new List<ISlotGroup>());
					}

					if (!tmpGroups[storageGroup.GroupingLabel].Contains(storageGroup)) {
						tmpGroups[storageGroup.GroupingLabel].Add(storageGroup);
					}
				} else if (slotGroup.parent is Building_Storage buildingStorage && buildingStorage is IRenameable) {
					if (!tmpGroups.ContainsKey(slotGroup.GroupingLabel)) {
						tmpGroups.Add(slotGroup.GroupingLabel, new List<ISlotGroup>());
					}

					tmpGroups[slotGroup.GroupingLabel].Add(slotGroup);
				}
			}

			return tmpGroups
				.OrderBy(kvp => kvp.Value.Count > 0 ? kvp.Value[0].GroupingOrder : 0)
				.SelectMany(kvp => kvp.Value);
		}

		public static string GetStorageGroupId(Map map, ISlotGroup group) {
			if (group == null) {
				return null;
			}

			if (group is StorageGroup) {
				List<SlotGroup> allGroups = map.haulDestinationManager.AllGroupsListInPriorityOrder;
				for (int i = 0; i < allGroups.Count; i++) {
					SlotGroup sg = allGroups[i];
					if (sg.StorageGroup == group) {
						IntVec3 cell = sg.CellsList.Any() ? sg.CellsList[0] : IntVec3.Invalid;
						return BISIds.StorageGroupCellPrefix + cell.x + "_" + cell.z;
					}
				}
				return null;
			}

			if (group is SlotGroup slotGroup) {
				Zone_Stockpile zone = slotGroup.parent as Zone_Stockpile;
				if (zone != null) {
					return BISIds.ZonePrefix + zone.ID;
				}

				IntVec3 cell = slotGroup.CellsList.Any() ? slotGroup.CellsList[0] : IntVec3.Invalid;
				return BISIds.SlotGroupPrefix + cell.x + "_" + cell.z;
			}

			return null;
		}

		public static bool IsStorageCompatibleWithBill(Bill_Production bill, Map map, ISlotGroup group) {
			if (bill == null || map == null || group == null || bill.recipe?.ingredients == null) {
				return false;
			}

			ThingFilter storageFilter = null;

			if (group is SlotGroup slotGroup) {
				storageFilter = slotGroup.Settings?.filter;
			} else if (group is StorageGroup) {
				// 같은 StorageGroup에 속한 아무 SlotGroup 하나의 필터를 대표로 사용
				List<SlotGroup> allGroups = map.haulDestinationManager.AllGroupsListInPriorityOrder;
				for (int i = 0; i < allGroups.Count; i++) {
					if (allGroups[i].StorageGroup == group) {
						storageFilter = allGroups[i].Settings?.filter;
						break;
					}
				}
			}

			if (storageFilter == null) {
				return false;
			}

			List<ThingDef> defs = DefDatabase<ThingDef>.AllDefsListForReading;
			for (int i = 0; i < bill.recipe.ingredients.Count; i++) {
				IngredientCount ingredient = bill.recipe.ingredients[i];
				if (ingredient?.filter == null) {
					continue;
				}

				for (int j = 0; j < defs.Count; j++) {
					ThingDef def = defs[j];
					if (ingredient.filter.Allows(def) && storageFilter.Allows(def)) {
						return true;
					}
				}
			}

			return false;
		}

		private static ISlotGroup FindStorageGroupByGroupingLabel(Map map, string groupingLabel) {
			if (map == null || string.IsNullOrEmpty(groupingLabel)) {
				return null;
			}

			List<SlotGroup> allGroups = map.haulDestinationManager.AllGroupsListInPriorityOrder;
			for (int i = 0; i < allGroups.Count; i++) {
				SlotGroup slotGroup = allGroups[i];
				if (slotGroup.StorageGroup != null && slotGroup.StorageGroup.GroupingLabel == groupingLabel) {
					return slotGroup.StorageGroup;
				}
			}

			return null;
		}

		private static Zone_Stockpile FindZoneById(Map map, int zoneId) {
			List<Zone> allZones = map.zoneManager.AllZones;
			for (int i = 0; i < allZones.Count; i++) {
				Zone_Stockpile zone = allZones[i] as Zone_Stockpile;
				if (zone != null && zone.ID == zoneId) {
					return zone;
				}
			}

			return null;
		}

		private static SlotGroup FindSlotGroupByStorageId(Map map, string storageId) {
			if (map == null || string.IsNullOrEmpty(storageId) || !storageId.StartsWith(BISIds.SlotGroupPrefix)) {
				return null;
			}

			string[] parts = storageId.Split('_');
			if (parts.Length != 3) {
				return null;
			}

			if (!int.TryParse(parts[1], out int x)) return null;
			if (!int.TryParse(parts[2], out int z)) return null;

			IntVec3 target = new IntVec3(x, 0, z);

			foreach (SlotGroup group in map.haulDestinationManager.AllGroupsListInPriorityOrder) {
				if (group.CellsList.Contains(target)) {
					return group;
				}
			}

			return null;
		}

		private static bool TryParseTailInt(string value, string prefix, out int number) {
			number = -1;

			if (string.IsNullOrEmpty(value) || !value.StartsWith(prefix)) {
				return false;
			}

			string tail = value.Substring(prefix.Length);
			return int.TryParse(tail, out number);
		}
	}
}