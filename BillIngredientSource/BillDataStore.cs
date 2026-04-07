using System.Collections.Generic;
using Verse;
using RimWorld;

namespace BillIngredientSource {
	public static class BillDataStore {
		private static readonly Dictionary<Bill, BillData> DataByBill = new Dictionary<Bill, BillData>();

		public static BillData GetOrCreate(Bill bill) {
			if (bill == null) {
				return null;
			}

			if (!DataByBill.TryGetValue(bill, out BillData data)) {
				data = new BillData();
				DataByBill[bill] = data;
			}

			return data;
		}

		public static bool TryGet(Bill bill, out BillData data) {
			return DataByBill.TryGetValue(bill, out data);
		}

		public static void Remove(Bill bill) {
			if (bill != null) {
				DataByBill.Remove(bill);
			}
		}

		public static void Clear() {
			DataByBill.Clear();
		}
	}
}