using HarmonyLib;
using RimWorld;
using Verse;

namespace BillFailureCooldown.Patches {
	[HarmonyPatch(typeof(WorkGiver_DoBill), "JobOnThing")]
	public static class Patch_WorkGiver_DoBill_JobOnThing {
		public static void Prefix(Pawn pawn, Thing thing, bool forced) {
			if (!BillFailureCooldownState.Enabled) {
				return;
			}

			if (!forced) {
				return;
			}

			if (thing == null) {
				return;
			}

			BillFailureCooldownState.ClearCooldownsForBillGiver(thing);
		}
	}
}