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

				// 중복 방지
				if (!seenThingIds.Add(thing.thingIDNumber)) {
					continue;
				}

				result.Add(thing);
			}
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