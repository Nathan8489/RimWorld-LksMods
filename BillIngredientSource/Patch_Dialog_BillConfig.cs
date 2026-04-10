using HarmonyLib;
using RimWorld;
using Verse;

namespace BillIngredientSource {
	[HarmonyPatch(typeof(Dialog_BillConfig), MethodType.Constructor, new[] { typeof(Bill_Production), typeof(IntVec3) })]
	public static class Patch_Dialog_BillConfig_Ctor {
		public static void Postfix(Bill_Production bill, IntVec3 billGiverPos) {
			var data = BillDataStore.GetOrCreate(bill);

			string storageText;
			if (data.UseAllStorages) {
				storageText = "__ALL_STORAGES__";
			} else if (data.SelectedStorageGroup != null) {
				storageText = SlotGroup.GetGroupLabel(data.SelectedStorageGroup);
			} else {
				storageText = "null";
			}

#if DEBUG
			Log.Message($"[BillIngredientSource] Dialog_BillConfig opened / mode={data.SearchMode}, storage={storageText}");
#endif
		}
	}
}