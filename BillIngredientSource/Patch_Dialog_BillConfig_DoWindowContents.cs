using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace BillIngredientSource {
	[HarmonyPatch(typeof(Dialog_BillConfig), "DoWindowContents")]
	public static class Patch_Dialog_BillConfig_DoWindowContents {
		public static void Postfix(Dialog_BillConfig __instance, Rect inRect) {
			Bill_Production bill = Traverse.Create(__instance).Field("bill").GetValue<Bill_Production>();
			if (bill == null) return;

			BillData data = BillDataStore.GetOrCreate(bill);

			float x = inRect.x;
			float y = inRect.y + 200f;

			float lineHeight = 30f;

			// 라벨
			Rect labelRect = new Rect(x, y, 140f, lineHeight);
			Widgets.Label(labelRect, "재료 탐색 방식");

			// 반경 버튼
			Rect radiusRect = new Rect(x + 150f, y, 100f, lineHeight);
			if (Widgets.RadioButtonLabeled(radiusRect, "반경", data.SearchMode == IngredientSearchMode.Radius)) {
				data.SearchMode = IngredientSearchMode.Radius;
			}

			// 저장소 버튼
			Rect storageRect = new Rect(x + 260f, y, 100f, lineHeight);
			if (Widgets.RadioButtonLabeled(storageRect, "저장소", data.SearchMode == IngredientSearchMode.Storage)) {
				data.SearchMode = IngredientSearchMode.Storage;
			}
		}
	}
}