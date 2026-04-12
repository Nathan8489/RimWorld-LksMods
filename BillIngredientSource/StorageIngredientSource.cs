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
			if (!BillDataStore.TryGet(bill, out data) || data == null) {
				return result;
			}

			EnsureMigrated(bill, data);

			HashSet<int> seenThingIds = new HashSet<int>();

			if (data.UseAllStorages) {
				AddAllStorageThings(map, result, seenThingIds);
				return result;
			}

			if (!ValidateSelectedStorage(map, bill, data)) {
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

			if (IsValidSlotGroup(map, slotGroup)) {
				return GetStableStorageLabel(slotGroup);
			}

			return "BIS_MissingStorage".Translate().ToString();
		}

		public static string GetStableStorageLabel(ISlotGroup slotGroup) {
			if (slotGroup == null) {
				return null;
			}

			SlotGroup sg = slotGroup as SlotGroup;
			if (sg != null) {
				Zone_Stockpile zone = sg.parent as Zone_Stockpile;
				if (zone != null) {
					return zone.label;
				}

				return SlotGroup.GetGroupLabel(sg);
			}

			StorageGroup group = slotGroup as StorageGroup;
			if (group != null) {
				return SlotGroup.GetGroupLabel(group);
			}

			return null;
		}

		public static SelectedStorageKind GetStorageKind(ISlotGroup slotGroup) {
			if (slotGroup == null) {
				return SelectedStorageKind.None;
			}

			SlotGroup sg = slotGroup as SlotGroup;
			if (sg != null) {
				if (sg.parent is Zone_Stockpile) {
					return SelectedStorageKind.Zone;
				}

				return SelectedStorageKind.SlotGroup;
			}

			if (slotGroup is StorageGroup) {
				return SelectedStorageKind.StorageGroup;
			}

			return SelectedStorageKind.None;
		}

		public static void AssignSelectedStorage(BillData data, ISlotGroup slotGroup) {
			if (data == null) {
				return;
			}

			data.UseAllStorages = false;
			data.SelectedStorageGroup = slotGroup;
			data.SelectedStorageLabel = GetStableStorageLabel(slotGroup);
			data.SelectedStorageKind = GetStorageKind(slotGroup);
			data.SearchMode = IngredientSearchMode.Storage;
			data.StorageResetNotified = false;
		}

		public static bool ValidateSelectedStorage(Map map, Bill_Production bill, BillData data) {
			if (map == null || data == null) {
				return false;
			}

			if (data.UseAllStorages) {
				return true;
			}

			if (IsValidSlotGroup(map, data.SelectedStorageGroup)) {
				// 메타데이터 보정
				if (string.IsNullOrEmpty(data.SelectedStorageLabel)) {
					data.SelectedStorageLabel = GetStableStorageLabel(data.SelectedStorageGroup);
				}
				if (data.SelectedStorageKind == SelectedStorageKind.None) {
					data.SelectedStorageKind = GetStorageKind(data.SelectedStorageGroup);
				}
				return true;
			}

			ISlotGroup rematched = TryRematchSelectedStorage(map, data);
			if (rematched != null) {
				data.SelectedStorageGroup = rematched;
				data.SelectedStorageLabel = GetStableStorageLabel(rematched);
				data.SelectedStorageKind = GetStorageKind(rematched);
				data.SearchMode = IngredientSearchMode.Storage;
				data.StorageResetNotified = false;

#if DEBUG
		if (Prefs.DevMode) {
			Log.Message("[BillIngredientSource] Rematched storage by name: " + data.SelectedStorageLabel);
		}
#endif
				return true;
			}

			string lostLabel = string.IsNullOrEmpty(data.SelectedStorageLabel)
				? "Unknown storage"
				: data.SelectedStorageLabel;

			string billLabel = GetBillLabel(bill);
			string benchLabel = GetBillGiverLabel(bill);

			data.ClearSelectedStorage();

			if (!data.StorageResetNotified) {
				Log.Warning(
					"[BillIngredientSource] Failed to rematch storage by name - bench=" + benchLabel +
					", bill=" + billLabel +
					", storage=" + lostLabel +
					". Resetting storage link for this bill."
				);

				Messages.Message(
					"[BIS] Storage reset: " + billLabel + " @ " + benchLabel + " (" + lostLabel + ")",
					MessageTypeDefOf.NeutralEvent
				);

				data.StorageResetNotified = true;
			}

			return false;
		}

		private static ISlotGroup TryRematchSelectedStorage(Map map, BillData data) {
			if (map == null || data == null) {
				return null;
			}

			if (string.IsNullOrEmpty(data.SelectedStorageLabel) || data.SelectedStorageKind == SelectedStorageKind.None) {
				return null;
			}

			if (data.SelectedStorageKind == SelectedStorageKind.Zone) {
				List<Zone> allZones = map.zoneManager.AllZones;
				for (int i = 0; i < allZones.Count; i++) {
					Zone_Stockpile zone = allZones[i] as Zone_Stockpile;
					if (zone != null && zone.label == data.SelectedStorageLabel) {
						return zone.GetSlotGroup();
					}
				}
				return null;
			}

			foreach (ISlotGroup group in GetAllSelectableStorageGroups(map)) {
				if (GetStorageKind(group) != data.SelectedStorageKind) {
					continue;
				}

				if (GetStableStorageLabel(group) == data.SelectedStorageLabel) {
					return group;
				}
			}

			return null;
		}

		private static bool IsValidSlotGroup(Map map, ISlotGroup slot) {
			if (map == null || slot == null) {
				return false;
			}

			SlotGroup slotGroup = slot as SlotGroup;
			if (slotGroup != null) {
				Zone_Stockpile zone = slotGroup.parent as Zone_Stockpile;
				if (zone != null) {
					return map.zoneManager.AllZones.Contains(zone);
				}

				return map.haulDestinationManager.AllGroups.Contains(slotGroup);
			}

			StorageGroup storageGroup = slot as StorageGroup;
			if (storageGroup != null) {
				return map.storageGroups.HasStorageGroup(storageGroup);
			}

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

			SlotGroup slotGroup = group as SlotGroup;
			if (slotGroup != null) {
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

				// Zone은 Dialog 쪽에서 별도로 추가하므로 여기서는 제외
				if (slotGroup.parent is Zone_Stockpile) {
					continue;
				}

				if (slotGroup.StorageGroup != null) {
					StorageGroup storageGroup = slotGroup.StorageGroup;
					if (!tmpGroups.ContainsKey(storageGroup.GroupingLabel)) {
						tmpGroups.Add(storageGroup.GroupingLabel, new List<ISlotGroup>());
					}

					if (!tmpGroups[storageGroup.GroupingLabel].Contains(storageGroup)) {
						tmpGroups[storageGroup.GroupingLabel].Add(storageGroup);
					}
				} else {
					Building_Storage buildingStorage = slotGroup.parent as Building_Storage;
					if (buildingStorage == null || buildingStorage is IRenameable) {
						if (!tmpGroups.ContainsKey(slotGroup.GroupingLabel)) {
							tmpGroups.Add(slotGroup.GroupingLabel, new List<ISlotGroup>());
						}

						tmpGroups[slotGroup.GroupingLabel].Add(slotGroup);
					}
				}
			}

			return tmpGroups
				.OrderBy(kvp => kvp.Value.Count > 0 ? kvp.Value[0].GroupingOrder : 0)
				.SelectMany(kvp => kvp.Value);
		}

		public static bool IsStorageCompatibleWithBill(Bill_Production bill, Map map, ISlotGroup group) {
			if (bill == null || map == null || group == null || bill.recipe == null || bill.recipe.ingredients == null) {
				return false;
			}

			ThingFilter storageFilter = null;

			SlotGroup slotGroup = group as SlotGroup;
			if (slotGroup != null) {
				storageFilter = slotGroup.Settings != null ? slotGroup.Settings.filter : null;
			} else if (group is StorageGroup) {
				List<SlotGroup> allGroups = map.haulDestinationManager.AllGroupsListInPriorityOrder;
				for (int i = 0; i < allGroups.Count; i++) {
					if (allGroups[i].StorageGroup == group) {
						storageFilter = allGroups[i].Settings != null ? allGroups[i].Settings.filter : null;
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
				if (ingredient == null || ingredient.filter == null) {
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

		private static void EnsureMigrated(Bill_Production bill, BillData data) {
			if (bill == null || data == null) {
				return;
			}

			// 이미 새 구조가 있으면 아무것도 안 함
			if (data.UseAllStorages || data.SelectedStorageGroup != null) {
				return;
			}

			// 레거시 데이터가 있으면 지금 즉시 마이그레이션
			if (data.HasLegacyData()) {
				LegacyStorageMigration.TryMigrate(bill, data);
			}
		}

		private static string GetBillLabel(Bill_Production bill) {
			if (bill == null) {
				return "Unknown bill";
			}

			string label = bill.LabelCap;
			return string.IsNullOrEmpty(label) ? "Unknown bill" : label;
		}

		private static string GetBillGiverLabel(Bill_Production bill) {
			if (bill != null && bill.billStack != null && bill.billStack.billGiver is Thing thing) {
				string label = thing.LabelShortCap;
				if (!string.IsNullOrEmpty(label)) {
					return label;
				}
			}

			return "Unknown bench";
		}
	}
}