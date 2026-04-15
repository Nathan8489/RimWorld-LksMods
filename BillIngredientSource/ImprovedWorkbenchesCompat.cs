using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace BillIngredientSource {
	[StaticConstructorOnStartup]
	public static class ImprovedWorkbenchesCompatBootstrap {
		static ImprovedWorkbenchesCompatBootstrap() {
			ImprovedWorkbenchesCompat.TryApplyPatches();
		}
	}

	public static class ImprovedWorkbenchesCompat {
		private static bool applied;

		public static void TryApplyPatches() {
			if (applied) {
				return;
			}

			Type storageType = AccessTools.TypeByName("ImprovedWorkbenches.ExtendedBillDataStorage");
			if (storageType == null) {
				BillIngredientSourceMod.DebugLog("Improved Workbenches not found.");
				return;
			}

			Harmony harmony = new Harmony("BillIngredientSource.ImprovedWorkbenchesCompat");

			// 실제 시그니처: MirrorBills(Bill_Production, Bill_Production, bool, bool)
			MethodInfo mirrorBills = AccessTools.Method(
				storageType,
				"MirrorBills",
				new[] { typeof(Bill_Production), typeof(Bill_Production), typeof(bool), typeof(bool) }
			);

			if (mirrorBills != null) {
				harmony.Patch(
					mirrorBills,
					postfix: new HarmonyMethod(typeof(ImprovedWorkbenchesCompat), nameof(MirrorBillsPostfix))
				);
				BillIngredientSourceMod.DebugLog("Patched Improved Workbenches MirrorBills.");
			} else {
				Log.Warning("[BillIngredientSource] Failed to find Improved Workbenches MirrorBills.");
			}

			MethodInfo linkBills = AccessTools.Method(
				storageType,
				"LinkBills",
				new[] { typeof(Bill_Production), typeof(Bill_Production) }
			);

			if (linkBills != null) {
				harmony.Patch(
					linkBills,
					postfix: new HarmonyMethod(typeof(ImprovedWorkbenchesCompat), nameof(LinkBillsPostfix))
				);
				BillIngredientSourceMod.DebugLog("Patched Improved Workbenches LinkBills.");
			} else {
				Log.Warning("[BillIngredientSource] Failed to find Improved Workbenches LinkBills.");
			}

			applied = true;
			Log.Message("[BillIngredientSource] Improved Workbenches compatibility patches applied.");
		}

		// 실제 메서드 시그니처와 맞춰서 받는 게 안전함
		public static void MirrorBillsPostfix(
			Bill_Production sourceBill,
			Bill_Production destinationBill,
			bool preserveTargetProduct,
			bool initialCopy) {

			CopyBISBillData(sourceBill, destinationBill);
		}

		public static void LinkBillsPostfix(Bill_Production parent, Bill_Production child) {
			CopyBISBillData(parent, child);
		}

		private static void CopyBISBillData(Bill_Production sourceBill, Bill_Production targetBill) {
			if (sourceBill == null || targetBill == null || sourceBill == targetBill) {
				return;
			}

			BillData sourceData = BillDataStore.GetOrCreate(sourceBill);
			if (sourceData == null) {
				return;
			}

			// 구버전 데이터가 아직 남아 있으면 먼저 가능한 범위에서 마이그레이션 시도
			if (!sourceData.UseAllStorages && sourceData.SelectedStorageGroup == null && sourceData.HasLegacyData()) {
				LegacyStorageMigration.TryMigrate(sourceBill, sourceData);
			}

			BillData copied = new BillData();
			copied.SearchMode = sourceData.SearchMode;
			copied.UseAllStorages = sourceData.UseAllStorages;
			copied.SelectedStorageGroup = sourceData.SelectedStorageGroup;
			copied.SelectedStorageLabel = sourceData.SelectedStorageLabel;
			copied.SelectedStorageKind = sourceData.SelectedStorageKind;

			// 알림 플래그는 새/동기화 대상 bill에서 다시 뜰 수 있도록 false
			copied.StorageResetNotified = false;

			// 레거시 데이터도 같이 넘겨서 구세이브에서도 안전성 유지
			copied.LegacySelectedStorageId = sourceData.LegacySelectedStorageId;
			copied.LegacySelectedZoneId = sourceData.LegacySelectedZoneId;
			copied.LegacySelectedZoneLabel = sourceData.LegacySelectedZoneLabel;

			BillDataStore.Set(targetBill, copied);

			BillIngredientSourceMod.DebugLog(
				"Improved Workbenches compat copied BIS data: " +
				GetBillDebugLabel(sourceBill) + " -> " + GetBillDebugLabel(targetBill)
			);
		}

		private static string GetBillDebugLabel(Bill_Production bill) {
			if (bill == null) {
				return "null";
			}

			string billLabel = bill.LabelCap;
			if (string.IsNullOrEmpty(billLabel)) {
				billLabel = "Unknown bill";
			}

			string giverLabel = "Unknown giver";
			if (bill.billStack != null && bill.billStack.billGiver is Thing thing) {
				giverLabel = thing.LabelShortCap;
			}

			return giverLabel + " / " + billLabel;
		}
	}
}