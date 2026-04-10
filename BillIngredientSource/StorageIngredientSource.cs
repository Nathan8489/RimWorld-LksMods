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

			if (!BillDataStore.TryGet(bill, out BillData data) || data == null) {
				return result;
			}

			HashSet<int> seenThingIds = new HashSet<int>();

			if (data.UseAllStorages) {
				AddAllStorageThings(map, result, seenThingIds);
				return result;
			}

			if (!ValidateSelectedStorage(map, data)) {
				return result;
			}

			if (data.SelectedStorageGroup != null) {
				AddISlotGroupThings(map, data.SelectedStorageGroup, result, seenThingIds);
			}

			return result;
		}

		public static string GetStorageLabel(Map map, ISlotGroup slotGroup) {
			if (slotGroup == null) {
				return null;
			}

			if (slotGroup is SlotGroup sg) {
				if (sg.parent is Zone_Stockpile zone) {
					if (map != null && map.zoneManager.AllZones.Contains(zone)) {
						return zone.label;
					}
					return "BIS_MissingZone".Translate().ToString();
				}

				if (map != null && map.haulDestinationManager.AllGroups.Contains(sg)) {
					return SlotGroup.GetGroupLabel(sg);
				}

				return "BIS_MissingStorage".Translate().ToString();
			}

			if (slotGroup is StorageGroup group) {
				if (map != null && map.storageGroups.HasStorageGroup(group)) {
					return SlotGroup.GetGroupLabel(group);
				}

				return "BIS_MissingStorage".Translate().ToString();
			}

			return "BIS_MissingStorage".Translate().ToString();
		}

		public static bool ValidateSelectedStorage(Map map, BillData data) {
			if (map == null || data == null || data.SelectedStorageGroup == null) {
				return false;
			}

			ISlotGroup slot = data.SelectedStorageGroup;

			if (slot is SlotGroup slotGroup) {
				if (slotGroup.parent is Zone_Stockpile zone) {
					if (zone == null || !map.zoneManager.AllZones.Contains(zone)) {
						data.SelectedStorageGroup = null;
						data.UseAllStorages = false;
						data.SearchMode = IngredientSearchMode.Radius;
						return false;
					}
					return true;
				}

				if (!map.haulDestinationManager.AllGroups.Contains(slotGroup)) {
					data.SelectedStorageGroup = null;
					data.UseAllStorages = false;
					data.SearchMode = IngredientSearchMode.Radius;
					return false;
				}

				return true;
			}

			if (slot is StorageGroup storageGroup) {
				if (!map.storageGroups.HasStorageGroup(storageGroup)) {
					data.SelectedStorageGroup = null;
					data.UseAllStorages = false;
					data.SearchMode = IngredientSearchMode.Radius;
					return false;
				}

				return true;
			}

			data.SelectedStorageGroup = null;
			data.UseAllStorages = false;
			data.SearchMode = IngredientSearchMode.Radius;
			return false;
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
				SlotGroup sg = allGroups[i];

				// Zone slot group는 위에서 이미 처리
				if (sg.parent is Zone_Stockpile) {
					continue;
				}

				AddSlotGroupThings(sg, result, seenThingIds);
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

			if (group is StorageGroup) {
				List<SlotGroup> allGroups = map.haulDestinationManager.AllGroupsListInPriorityOrder;
				for (int i = 0; i < allGroups.Count; i++) {
					SlotGroup sg = allGroups[i];
					if (sg.StorageGroup == group) {
						AddSlotGroupThings(sg, result, seenThingIds);
					}
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
				} else if (!(slotGroup.parent is Building_Storage buildingStorage) || buildingStorage is IRenameable) {
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

		public static bool IsStorageCompatibleWithBill(Bill_Production bill, Map map, ISlotGroup group) {
			if (bill == null || map == null || group == null || bill.recipe?.ingredients == null) {
				return false;
			}

			ThingFilter storageFilter = null;

			if (group is SlotGroup slotGroup) {
				storageFilter = slotGroup.Settings?.filter;
			} else if (group is StorageGroup) {
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
	}
}