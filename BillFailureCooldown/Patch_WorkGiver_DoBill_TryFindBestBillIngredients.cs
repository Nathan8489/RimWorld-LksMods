using HarmonyLib;
using RimWorld;
using Verse;

namespace BillFailureCooldown.Patches {
	[HarmonyPatch(typeof(WorkGiver_DoBill), "TryFindBestBillIngredients")]
	public static class Patch_WorkGiver_DoBill_TryFindBestBillIngredients {
		public static bool Prefix(Bill bill, ref bool __result) {
			if (!BillFailureCooldownState.Enabled) {
				return true;
			}

			if (bill == null) {
				return true;
			}

			int currentTick = Find.TickManager.TicksGame;

			if (BillFailureCooldownState.IsCoolingDown(bill, currentTick)) {
				__result = false;
				return false;
			}

			return true;
		}

		public static void Postfix(Bill bill, bool __result) {
			if (!BillFailureCooldownState.Enabled) {
				return;
			}

			if (bill == null) {
				return;
			}

			int currentTick = Find.TickManager.TicksGame;

			if (__result) {
				BillFailureCooldownState.ClearCooldown(bill);
			} else {
				BillFailureCooldownState.StartCooldown(bill, currentTick);
			}
		}
	}
}