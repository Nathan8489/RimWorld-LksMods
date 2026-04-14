using UnityEngine;
using Verse;

namespace BillIngredientSource {
	public class BillIngredientSourceMod : Mod {
		public static BillIngredientSourceSettings Settings;

		public BillIngredientSourceMod(ModContentPack content) : base(content) {
			Settings = GetSettings<BillIngredientSourceSettings>();
		}

		public override string SettingsCategory() {
			return "Bill Ingredient Source";
		}

		public override void DoSettingsWindowContents(Rect inRect) {
			Listing_Standard listing = new Listing_Standard();
			listing.Begin(inRect);

			listing.Label("BIS_Settings_DefaultNewBillMode".Translate());
			listing.Gap(6f);

			Rect vanillaRect = listing.GetRect(28f);
			bool vanillaSelected = Settings.defaultNewBillMode == NewBillDefaultMode.Vanilla;
			if (Widgets.RadioButtonLabeled(vanillaRect, "BIS_Settings_DefaultNewBillMode_Vanilla".Translate(), vanillaSelected)) {
				Settings.defaultNewBillMode = NewBillDefaultMode.Vanilla;
			}

			Rect allStoragesRect = listing.GetRect(28f);
			bool allStoragesSelected = Settings.defaultNewBillMode == NewBillDefaultMode.AllStorages;
			if (Widgets.RadioButtonLabeled(allStoragesRect, "BIS_Settings_DefaultNewBillMode_AllStorages".Translate(), allStoragesSelected)) {
				Settings.defaultNewBillMode = NewBillDefaultMode.AllStorages;
			}

			listing.End();
			Settings.Write();
		}

		public static bool DebugLoggingEnabled {
			get {
				return Settings != null && Settings.enableDebugLogging;
			}
		}

		public static void DebugLog(string message) {
			if (DebugLoggingEnabled) {
				Log.Message("[BillIngredientSource] " + message);
			}
		}
	}
}
