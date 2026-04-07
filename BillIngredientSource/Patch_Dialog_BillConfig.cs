using HarmonyLib;
using RimWorld;
using Verse;

namespace BillIngredientSource {
	[HarmonyPatch(typeof(Dialog_BillConfig), MethodType.Constructor, new[] { typeof(Bill_Production), typeof(IntVec3) })]
	public static class Patch_Dialog_BillConfig_Ctor {
		public static void Postfix(Bill_Production bill, IntVec3 billGiverPos) {
			var data = BillDataStore.GetOrCreate(bill);
			Log.Message($"[BillIngredientSource] Dialog_BillConfig opened / mode={data.SearchMode}, storage={data.SelectedStorageId ?? "null"}");
		}
	}
}