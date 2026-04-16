using System.Collections.Generic;
using System.Runtime.CompilerServices;
using RimWorld;

namespace BillIngredientSource {
	public static class BillDataStore {
		private static readonly ConditionalWeakTable<Bill, BillData> dataByBill = new ConditionalWeakTable<Bill, BillData>();

		public static BillData GetOrCreate(Bill bill) {
			return dataByBill.GetOrCreateValue(bill);
		}

		public static bool TryGet(Bill bill, out BillData data) {
			return dataByBill.TryGetValue(bill, out data);
		}

		public static void Set(Bill bill, BillData data) {
			if (bill == null) {
				return;
			}

			dataByBill.AddOrUpdate(bill, data ?? new BillData());
		}

		public static void Remove(Bill bill) {
			dataByBill.Remove(bill);
		}

		public static void Clear() {
			dataByBill.Clear();
		}
	}
}
