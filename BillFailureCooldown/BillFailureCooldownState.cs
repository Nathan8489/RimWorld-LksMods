using System.Collections.Generic;
using RimWorld;
using Verse;

namespace BillFailureCooldown {
	public static class BillFailureCooldownState {
		private static readonly Dictionary<string, int> CooldownUntilByBillId = new Dictionary<string, int>();

		public static bool Enabled {
			get {
				return BillFailureCooldownMod.Settings != null
					&& BillFailureCooldownMod.Settings.searchFailureCooldownTicks > 0;
			}
		}

		public static bool IsCoolingDown(Bill bill, int currentTick) {
			if (bill == null) {
				return false;
			}

			string key = GetBillKey(bill);
			int untilTick;
			if (!CooldownUntilByBillId.TryGetValue(key, out untilTick)) {
				return false;
			}

			if (untilTick <= currentTick) {
				CooldownUntilByBillId.Remove(key);
				return false;
			}

			return true;
		}

		public static void StartCooldown(Bill bill, int currentTick) {
			if (bill == null || !Enabled) {
				return;
			}

			string key = GetBillKey(bill);
			int untilTick = currentTick + BillFailureCooldownMod.Settings.searchFailureCooldownTicks;
			CooldownUntilByBillId[key] = untilTick;
		}

		public static void ClearCooldown(Bill bill) {
			if (bill == null) {
				return;
			}

			string key = GetBillKey(bill);
			CooldownUntilByBillId.Remove(key);
		}

		public static void ClearCooldownsForBillGiver(Thing billGiver) {
			IBillGiver billGiverInterface = billGiver as IBillGiver;
			if (billGiverInterface == null) {
				return;
			}

			BillStack billStack = billGiverInterface.BillStack;
			if (billStack == null) {
				return;
			}

			for (int i = 0; i < billStack.Count; i++) {
				Bill bill = billStack[i];
				ClearCooldown(bill);
			}
		}

		private static string GetBillKey(Bill bill) {
			return bill.GetUniqueLoadID();
		}
	}
}