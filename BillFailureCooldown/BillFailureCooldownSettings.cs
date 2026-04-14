using Verse;

namespace BillFailureCooldown {
	public sealed class BillFailureCooldownSettings : ModSettings {
		public const int DefaultSearchFailureCooldownTicks = 180;

		public int searchFailureCooldownTicks = DefaultSearchFailureCooldownTicks;

		public override void ExposeData() {
			Scribe_Values.Look(ref searchFailureCooldownTicks, "searchFailureCooldownTicks", DefaultSearchFailureCooldownTicks);
			base.ExposeData();
		}

		public void Reset() {
			searchFailureCooldownTicks = DefaultSearchFailureCooldownTicks;
		}
	}
}