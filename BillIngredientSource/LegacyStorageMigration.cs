using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace BillIngredientSource {
	public static class LegacyStorageMigration {
		public static void TryMigrate(Bill_Production bill, BillData data) {
			if (bill == null || data == null) {
				return;
			}

			// 이미 새 구조가 있으면 레거시 정리
			if (data.UseAllStorages || data.SelectedStorageGroup != null) {
				data.ClearLegacyData();
				return;
			}

			if (!data.HasLegacyData()) {
				return;
			}

			Map map = GetBillMap(bill);
			if (map == null) {
				return;
			}

			// 1) AllStorages
			if (data.LegacySelectedStorageId == BISIds.AllStorages) {
				data.UseAllStorages = true;
				data.SelectedStorageGroup = null;
				data.SearchMode = IngredientSearchMode.Storage;
				data.ClearLegacyData();
#if DEBUG
				Log.Message("[BillIngredientSource] Migrated legacy storage: ALL");
#endif
				return;
			}

			// 2) Zone_<id>
			if (!string.IsNullOrEmpty(data.LegacySelectedStorageId) &&
				data.LegacySelectedStorageId.StartsWith(BISIds.ZonePrefix)) {

				if (TryParseTailInt(data.LegacySelectedStorageId, BISIds.ZonePrefix, out int zoneId)) {
					Zone_Stockpile zone = map.zoneManager.AllZones
						.OfType<Zone_Stockpile>()
						.FirstOrDefault(z => z.ID == zoneId);

					if (zone != null) {
						StorageIngredientSource.AssignSelectedStorage(data, zone.GetSlotGroup());
						data.ClearLegacyData();
#if DEBUG
						Log.Message("[BillIngredientSource] Migrated legacy zone by ID: " + zone.label);
#endif
						return;
					}
				}

				// ID 실패 시 label fallback
				if (!string.IsNullOrEmpty(data.LegacySelectedZoneLabel)) {
					Zone_Stockpile zoneByLabel = map.zoneManager.AllZones
						.OfType<Zone_Stockpile>()
						.FirstOrDefault(z => z.label == data.LegacySelectedZoneLabel);

					if (zoneByLabel != null) {
						StorageIngredientSource.AssignSelectedStorage(data, zoneByLabel.GetSlotGroup());
						data.ClearLegacyData();
#if DEBUG
						Log.Message("[BillIngredientSource] Migrated legacy zone by label: " + zoneByLabel.label);
#endif
						return;
					}
				}
			}

			// 3) SlotGroup_x_z / StorageGroupCell_x_z
			if (!string.IsNullOrEmpty(data.LegacySelectedStorageId)) {
				ISlotGroup migratedGroup = FindLegacyGroup(map, data.LegacySelectedStorageId);
				if (migratedGroup != null) {
					StorageIngredientSource.AssignSelectedStorage(data, migratedGroup);
					data.ClearLegacyData();
#if DEBUG
					Log.Message("[BillIngredientSource] Migrated legacy storage group: " + SlotGroup.GetGroupLabel(migratedGroup));
#endif
					return;
				}
			}

			// 실패: 새 구조는 비워 두되, 레거시 데이터는 남겨서 다음 접근 시 재시도 가능하게 한다.
			Log.Warning("[BillIngredientSource] Failed to migrate legacy storage selection for now. Keeping legacy data for retry.");
			data.UseAllStorages = false;
			data.SelectedStorageGroup = null;
			data.SelectedStorageLabel = null;
			data.SelectedStorageKind = SelectedStorageKind.None;
			data.SearchMode = IngredientSearchMode.Radius;
		}

		private static Map GetBillMap(Bill_Production bill) {
			if (bill != null && bill.billStack != null && bill.billStack.billGiver is Thing thing) {
				return thing.Map;
			}
			return null;
		}

		private static ISlotGroup FindLegacyGroup(Map map, string storageId) {
			if (map == null || string.IsNullOrEmpty(storageId)) {
				return null;
			}

			if (storageId.StartsWith(BISIds.StorageGroupCellPrefix) || storageId.StartsWith(BISIds.SlotGroupPrefix)) {
				string[] parts = storageId.Split('_');
				if (parts.Length == 3 &&
					int.TryParse(parts[1], out int x) &&
					int.TryParse(parts[2], out int z)) {

					IntVec3 target = new IntVec3(x, 0, z);
					List<SlotGroup> allGroups = map.haulDestinationManager.AllGroupsListInPriorityOrder;

					for (int i = 0; i < allGroups.Count; i++) {
						SlotGroup sg = allGroups[i];
						if (sg.CellsList.Contains(target)) {
							if (storageId.StartsWith(BISIds.StorageGroupCellPrefix)) {
								return sg.StorageGroup ?? (ISlotGroup)sg;
							}
							return sg;
						}
					}
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