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
			if (data.SelectedStorageId == BillData.AllStoragesId) {
				AddAllStorageThings(map, result, seenThingIds);
				return result;
			}

			// 특정 zone
			if (!string.IsNullOrEmpty(data.SelectedStorageId) && data.SelectedStorageId.StartsWith("Zone_")) {
				int zoneId;
				if (TryParseTailInt(data.SelectedStorageId, "Zone_", out zoneId)) {
					Zone_Stockpile zone = FindZoneById(map, zoneId);
					if (zone != null) {
						AddSlotGroupThings(zone.GetSlotGroup(), result, seenThingIds);
					}
				}
				return result;
			}

			// 특정 storage building (선반 포함)
			if (!string.IsNullOrEmpty(data.SelectedStorageId) && data.SelectedStorageId.StartsWith("Building_")) {
				int thingId;
				if (TryParseTailInt(data.SelectedStorageId, "Building_", out thingId)) {
					Building_Storage storage = FindStorageBuildingByThingId(map, thingId);
					if (storage != null) {
						AddSlotGroupThings(storage.GetSlotGroup(), result, seenThingIds);
					}
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

			if (storageId == BillData.AllStoragesId) {
				return "(모든 저장소)";
			}

			if (map != null && storageId.StartsWith("Zone_")) {
				int zoneId;
				if (TryParseTailInt(storageId, "Zone_", out zoneId)) {
					Zone_Stockpile zone = FindZoneById(map, zoneId);
					if (zone != null) {
						return string.IsNullOrEmpty(zone.label) ? "(이름 없음)" : zone.label;
					}
					return "(없어진 저장구역)";
				}
			}

			if (map != null && storageId.StartsWith("Building_")) {
				int thingId;
				if (TryParseTailInt(storageId, "Building_", out thingId)) {
					Building_Storage storage = FindStorageBuildingByThingId(map, thingId);
					if (storage != null) {
						return GetStorageBuildingLabel(storage);
					}
					return "(없어진 저장소)";
				}
			}

			return fallbackLabel;
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

			return "Building_" + storage.thingIDNumber;
		}

		public static string GetStorageBuildingLabel(Building_Storage storage) {
			if (storage == null) {
				return "(없음)";
			}

			string baseLabel = storage.LabelCap;
			if (string.IsNullOrWhiteSpace(baseLabel)) {
				baseLabel = storage.def?.label ?? "storage";
			}

			return baseLabel + " (" + storage.Position.x + ", " + storage.Position.z + ")";
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
	}
}