using HarmonyLib;
using Verse;

namespace BillIngredientSource {
	[StaticConstructorOnStartup]
	public static class Init {
		static Init() {
			var harmony = new Harmony("you.billingredientsource");
			harmony.PatchAll();

			Log.Message("[BillIngredientSource] Loaded");
		}
	}
}