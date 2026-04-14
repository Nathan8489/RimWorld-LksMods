using HarmonyLib;
using UnityEngine;
using Verse;

namespace BillFailureCooldown {
	public sealed class BillFailureCooldownMod : Mod {
		public static BillFailureCooldownSettings Settings;

		public BillFailureCooldownMod(ModContentPack content) : base(content) {
			Settings = GetSettings<BillFailureCooldownSettings>();

			Harmony harmony = new Harmony("lk.billfailurecooldown");
			harmony.PatchAll();
		}

		public override string SettingsCategory() {
			return "BFC_ModName".Translate();
		}

		public override void DoSettingsWindowContents(Rect inRect) {
			Listing_Standard listing = new Listing_Standard();
			listing.Begin(inRect);

			listing.Label("BFC_SearchFailureCooldownTicks".Translate(Settings.searchFailureCooldownTicks));
			Settings.searchFailureCooldownTicks =
				Mathf.RoundToInt(listing.Slider(Settings.searchFailureCooldownTicks, 0f, 900f));
			Settings.searchFailureCooldownTicks =
				Mathf.Clamp(Settings.searchFailureCooldownTicks, 0, 900);

			listing.Gap();
			listing.Label("BFC_SearchFailureCooldownDesc".Translate());

			listing.Gap();

			if (listing.ButtonText("BFC_ResetToDefault".Translate())) {
				Settings.Reset();
			}

			listing.End();

			base.DoSettingsWindowContents(inRect);
		}
	}
}