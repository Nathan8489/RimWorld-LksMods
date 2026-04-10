using HarmonyLib;
using RimWorld;

namespace BillIngredientSource {
	[HarmonyPatch(typeof(Bill_Production), "Clone")]
	public static class Patch_Bill_Production_Clone {
		public static void Postfix(Bill_Production __instance, Bill __result) {
			Bill_Production clonedBill = __result as Bill_Production;
			if (clonedBill == null) {
				return;
			}

			if (!BillDataStore.TryGet(__instance, out BillData originalData) || originalData == null) {
				return;
			}

			BillData copied = new BillData();
			copied.SearchMode = originalData.SearchMode;
			copied.UseAllStorages = originalData.UseAllStorages;
			copied.SelectedStorageGroup = originalData.SelectedStorageGroup;
			copied.SelectedStorageLabel = originalData.SelectedStorageLabel;
			copied.SelectedStorageKind = originalData.SelectedStorageKind;
			copied.LegacySelectedStorageId = originalData.LegacySelectedStorageId;
			copied.LegacySelectedZoneId = originalData.LegacySelectedZoneId;
			copied.LegacySelectedZoneLabel = originalData.LegacySelectedZoneLabel;
			BillDataStore.Set(clonedBill, copied);
		}
	}
}