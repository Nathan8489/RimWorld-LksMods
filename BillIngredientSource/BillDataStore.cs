using System.Collections.Generic;
using RimWorld;

namespace BillIngredientSource {
	public static class BillDataStore {
		private static readonly Dictionary<Bill, BillData> dataByBill = new Dictionary<Bill, BillData>();

		public static BillData GetOrCreate(Bill bill) {
			if (!dataByBill.TryGetValue(bill, out var data)) {
				data = new BillData();
				dataByBill[bill] = data;
			}
			return data;
		}

		public static bool TryGet(Bill bill, out BillData data) {
			return dataByBill.TryGetValue(bill, out data);
		}

		public static void Set(Bill bill, BillData data) {
			if (bill == null) {
				return;
			}

			dataByBill[bill] = data ?? new BillData();
		}

		public static void Remove(Bill bill) {
			dataByBill.Remove(bill);
		}

		public static void Clear() {
			dataByBill.Clear();
		}
	}
}
