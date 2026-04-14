using Verse;

namespace BillFailureCooldown {
	public sealed class BillFailureCooldownSettings : ModSettings {
		public int searchFailureCooldownTicks = 180;

		public override void ExposeData() {
			Scribe_Values.Look(ref searchFailureCooldownTicks, "searchFailureCooldownTicks", 180);
			base.ExposeData();
		}
	}
}