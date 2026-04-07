using System.Collections.Generic;
using System.Linq;
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

			// 우측 상단, 바닐라 재료 탐색 범위 근처에 맞춘 임시 좌표
			float baseX = inRect.x + 660f;
			float baseY = inRect.y + 60f;
			float lineHeight = 28f;

			// 라벨
			Rect labelRect = new Rect(baseX - 170f, baseY, 120f, lineHeight);
			Widgets.Label(labelRect, "재료 탐색 방식");

			// 반경 라디오
			Rect radiusButtonRect = new Rect(baseX - 40f, baseY + 4f, 24f, 24f);
			if (Widgets.RadioButton(radiusButtonRect.position, data.SearchMode == IngredientSearchMode.Radius)) {
				data.SearchMode = IngredientSearchMode.Radius;
			}

			Rect radiusLabelRect = new Rect(baseX - 12f, baseY, 40f, lineHeight);
			Widgets.Label(radiusLabelRect, "반경");

			// 저장소 라디오
			Rect storageButtonRect = new Rect(baseX + 45f, baseY + 4f, 24f, 24f);
			if (Widgets.RadioButton(storageButtonRect.position, data.SearchMode == IngredientSearchMode.Storage)) {
				data.SearchMode = IngredientSearchMode.Storage;
			}

			Rect storageLabelRect = new Rect(baseX + 73f, baseY, 60f, lineHeight);
			Widgets.Label(storageLabelRect, "저장소");

			// 저장소 모드일 때만 버튼 표시
			if (data.SearchMode == IngredientSearchMode.Storage) {
				float buttonY = baseY + 34f;
				Rect storageSelectRect = new Rect(baseX - 170f, buttonY, 260f, 30f);

				string buttonLabel = string.IsNullOrEmpty(data.SelectedZoneLabel)
					? "재료 저장소 선택"
					: "재료 저장소: " + data.SelectedZoneLabel;

				if (Widgets.ButtonText(storageSelectRect, buttonLabel)) {
					List<FloatMenuOption> options = new List<FloatMenuOption>();

					Map map = Find.CurrentMap;
					if (map != null) {
						List<Zone_Stockpile> zones = map.zoneManager.AllZones
							.OfType<Zone_Stockpile>()
							.OrderBy(z => z.label)
							.ToList();

						foreach (Zone_Stockpile zone in zones) {
							Zone_Stockpile localZone = zone;
							string optionLabel = string.IsNullOrEmpty(localZone.label)
								? "(이름 없음)"
								: localZone.label;

							options.Add(new FloatMenuOption(optionLabel, delegate {
								data.SelectedZoneId = localZone.ID;
								data.SelectedZoneLabel = localZone.label;
								data.SelectedStorageId = "Zone_" + localZone.ID;
								Log.Message("[BillIngredientSource] Selected zone: " + data.SelectedStorageId);
							}));
						}
					}

					if (options.Count == 0) {
						options.Add(new FloatMenuOption("(선택 가능한 저장구역 없음)", null));
					}

					Find.WindowStack.Add(new FloatMenu(options));
				}
			}
		}
	}
}