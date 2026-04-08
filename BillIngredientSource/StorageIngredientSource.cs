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

			BillData data;
			if (!BillDataStore.TryGet(bill, out data)) {
				return result;
			}

			if (data.SearchMode != IngredientSearchMode.Storage) {
				return result;
			}

			HashSet<int> seenThingIds = new HashSet<int>();

			// 모든 저장소
			if (data.SelectedStorageId == BISIds.AllStorages) {
				AddAllStorageThings(map, result, seenThingIds);
				return result;
			}

			// 특정 zone
			if (!string.IsNullOrEmpty(data.SelectedStorageId) && data.SelectedStorageId.StartsWith(BISIds.ZonePrefix)) {
				int zoneId;
				if (TryParseTailInt(data.SelectedStorageId, BISIds.ZonePrefix, out zoneId)) {
					Zone_Stockpile zone = FindZoneById(map, zoneId);
					if (zone != null) {
						AddSlotGroupThings(zone.GetSlotGroup(), result, seenThingIds);
					}
				}
				return result;
			}

			// 특정 storage building (선반 포함)
			if (!string.IsNullOrEmpty(data.SelectedStorageId) && data.SelectedStorageId.StartsWith(BISIds.BuildingPrefix)) {
				int thingId;
				if (TryParseTailInt(data.SelectedStorageId, BISIds.BuildingPrefix, out thingId)) {
					Building_Storage storage = FindStorageBuildingByThingId(map, thingId);
					if (storage != null) {
						AddSlotGroupThings(storage.GetSlotGroup(), result, seenThingIds);
					}
				}
				return result;
			}

			// 특정 SlotGroup (연결 선반 포함)
			if (!string.IsNullOrEmpty(data.SelectedStorageId) && data.SelectedStorageId.StartsWith(BISIds.SlotGroupPrefix)) {
				SlotGroup slotGroup = FindSlotGroupByStorageId(map, data.SelectedStorageId);
				if (slotGroup != null) {
					AddSlotGroupThings(slotGroup, result, seenThingIds);
				}
				return result;
			}

			return result;
		}

		private static void AddAllStorageThings(Map map, List<Thing> result, HashSet<int> seenThingIds) {
			// stockpile zones
			List<Zone> allZones = map.zoneManager.AllZones;
			for (int i = 0; i < allZones.Count; i++) {
				Zone_Stockpile zone = allZones[i] as Zone_Stockpile;
				if (zone != null) {
					AddSlotGroupThings(zone.GetSlotGroup(), result, seenThingIds);
				}
			}

			// storage buildings (shelf 포함)
			IEnumerable<Building_Storage> storages = map.listerBuildings.AllBuildingsColonistOfClass<Building_Storage>();
			foreach (Building_Storage storage in storages) {
				if (storage != null && storage.Spawned) {
					AddSlotGroupThings(storage.GetSlotGroup(), result, seenThingIds);
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
				return "(모든 저장소)";
			}

			if (map != null && storageId.StartsWith(BISIds.ZonePrefix)) {
				int zoneId;
				if (TryParseTailInt(storageId, BISIds.ZonePrefix, out zoneId)) {
					Zone_Stockpile zone = FindZoneById(map, zoneId);
					if (zone != null) {
						return string.IsNullOrEmpty(zone.label) ? "(이름 없음)" : zone.label;
					}
					return "(없어진 저장구역)";
				}
			}

			if (map != null && storageId.StartsWith(BISIds.BuildingPrefix)) {
				int thingId;
				if (TryParseTailInt(storageId, BISIds.BuildingPrefix, out thingId)) {
					Building_Storage storage = FindStorageBuildingByThingId(map, thingId);
					if (storage != null) {
						return GetStorageBuildingLabel(storage);
					}
					return "(없어진 저장소)";
				}
			}

			if (map != null && storageId.StartsWith(BISIds.SlotGroupPrefix)) {
				SlotGroup slotGroup = FindSlotGroupByStorageId(map, storageId);
				if (slotGroup != null) {
					return GetSlotGroupLabel(slotGroup);
				}
				return "(없어진 저장소)";
			}

			return fallbackLabel;
		}

		public static IEnumerable<SlotGroup> GetAllStorageSlotGroups(Map map) {
			if (map == null) {
				return Enumerable.Empty<SlotGroup>();
			}

			return map.haulDestinationManager.AllGroupsListInPriorityOrder
				.OfType<SlotGroup>()
				.Where(sg => sg.parent is Building_Storage)
				.Where(HasCustomStorageGroupLabel)
				.GroupBy(GetSlotGroupStorageId)
				.Select(g => g.First())
				.OrderBy(GetSlotGroupLabel);
		}

		private static bool HasCustomStorageGroupLabel(SlotGroup slotGroup) {
			string label;
			return TryGetCustomStorageGroupLabel(slotGroup, out label);
		}

		public static string GetSlotGroupStorageId(SlotGroup slotGroup) {
			if (slotGroup == null) {
				return null;
			}

			IntVec3 cell = slotGroup.CellsList.Any() ? slotGroup.CellsList[0] : IntVec3.Invalid;
			return BISIds.SlotGroupPrefix + cell.x + "_" + cell.z;
		}

		public static string GetSlotGroupLabel(SlotGroup slotGroup) {
			if (slotGroup == null) {
				return "(없음)";
			}

			string label;
			return TryGetCustomStorageGroupLabel(slotGroup, out label) ? label : null;
		}

		private static bool TryGetCustomStorageGroupLabel(SlotGroup slotGroup, out string label) {
			label = null;

			if (slotGroup == null) {
				return false;
			}

			if (!(slotGroup.parent is Building_Storage storage)) {
				return false;
			}

			// 1순위: 직접 지정된 커스텀 이름
			if (!string.IsNullOrWhiteSpace(storage.label)) {
				label = storage.label;
				return true;
			}

			// 2순위: 표시 라벨이 기본 def 라벨과 다르면 커스텀 이름으로 간주
			string capLabel = storage.LabelCap;
			string defLabel = storage.def?.label;

			if (!string.IsNullOrWhiteSpace(capLabel)) {
				if (string.IsNullOrWhiteSpace(defLabel) ||
					!capLabel.Equals(defLabel, StringComparison.OrdinalIgnoreCase)) {
					label = capLabel;
					return true;
				}
			}

			return false;
		}

		public static IEnumerable<Building_Storage> GetAllStorageBuildings(Map map) {
			if (map == null) {
				return Enumerable.Empty<Building_Storage>();
			}

			return map.listerBuildings.AllBuildingsColonistOfClass<Building_Storage>()
				.Where(b => b != null && b.Spawned)
				.OrderBy(GetStorageBuildingLabel);
		}

		public static string GetStorageBuildingId(Building_Storage storage) {
			if (storage == null) {
				return null;
			}

			SlotGroup slotGroup = storage.GetSlotGroup();
			if (slotGroup == null) {
				return null;
			}

			IntVec3 cell = slotGroup.CellsList.Any() ? slotGroup.CellsList[0] : storage.Position;
			return BISIds.SlotGroupPrefix + cell.x + "_" + cell.z;
		}

		public static string GetStorageBuildingLabel(Building_Storage storage) {
			if (storage == null) {
				return "(없음)";
			}

			string baseLabel = storage.LabelCap;
			if (string.IsNullOrWhiteSpace(baseLabel)) {
				baseLabel = storage.def?.label ?? "storage";
			}

			return baseLabel;
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

			foreach (SlotGroup group in map.haulDestinationManager.AllGroupsListInPriorityOrder.OfType<SlotGroup>()) {
				if (group.CellsList.Contains(target)) {
					return group;
				}
			}

			return null;
		}

		private static Building_Storage FindStorageBuildingByThingId(Map map, int thingId) {
			if (map == null) {
				return null;
			}

			List<Thing> things = map.listerThings.AllThings;
			for (int i = 0; i < things.Count; i++) {
				Building_Storage storage = things[i] as Building_Storage;
				if (storage != null && storage.thingIDNumber == thingId) {
					return storage;
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

		public static bool IsStorageCompatibleWithBill(Bill_Production bill, SlotGroup slotGroup) {
			if (bill == null || slotGroup == null || bill.recipe?.ingredients == null) {
				return false;
			}

			StorageSettings settings = slotGroup.Settings;
			if (settings == null) {
				return false;
			}

			ThingFilter storageFilter = settings.filter;
			if (storageFilter == null) {
				return false;
			}

			for (int i = 0; i < bill.recipe.ingredients.Count; i++) {
				IngredientCount ingredient = bill.recipe.ingredients[i];
				if (ingredient?.filter == null) {
					continue;
				}

				List<ThingDef> defs = DefDatabase<ThingDef>.AllDefsListForReading;
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