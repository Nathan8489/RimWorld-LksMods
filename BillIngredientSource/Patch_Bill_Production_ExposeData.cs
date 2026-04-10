using HarmonyLib;
using RimWorld;
using Verse;

namespace BillIngredientSource {
	[HarmonyPatch(typeof(Bill_Production), "ExposeData")]
	public static class Patch_Bill_Production_ExposeData {
		public static void Postfix(Bill_Production __instance) {
			BillData data = BillDataStore.GetOrCreate(__instance);
			Scribe_Deep.Look(ref data, "BillIngredientSource_Data");

			if (data == null) {
				data = new BillData();
			}

			BillDataStore.Set(__instance, data);

			if (Scribe.mode == LoadSaveMode.PostLoadInit) {
				LegacyStorageMigration.TryMigrate(__instance, data);
				BillDataStore.Set(__instance, data);
			}
		}
	}
}