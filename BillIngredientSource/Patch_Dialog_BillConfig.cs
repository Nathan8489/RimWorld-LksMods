using HarmonyLib;
using RimWorld;
using Verse;

namespace BillIngredientSource {
	[HarmonyPatch(typeof(Dialog_BillConfig), MethodType.Constructor, typeof(Bill_Production))]
	public static class Patch_Dialog_BillConfig_Ctor {
		public static void Postfix(Bill_Production bill) {
			BillData data = BillDataStore.GetOrCreate(bill);
			Log.Message($"[BillIngredientSource] Bill config opened / mode={data.SearchMode}, storageId={data.SelectedStorageId ?? "null"}");
		}
	}
}