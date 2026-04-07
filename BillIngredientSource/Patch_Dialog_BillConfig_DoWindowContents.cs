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

			// ===== 1. 저장소 모드일 때 바닐라 반경 UI 숨기기 =====
			if (data.SearchMode == IngredientSearchMode.Storage) {
				// 이 좌표는 현재 1.6 UI 기준으로 맞춰가는 임시값
				Rect vanillaRadiusArea = new Rect(inRect.x + 640f, inRect.y + 28f, 330f, 70f);

				// 배경색으로 덮기
				Widgets.DrawBoxSolid(vanillaRadiusArea, new Color(0.07f, 0.08f, 0.09f, 1f));

				// 클릭 막기
				Widgets.ButtonInvisible(vanillaRadiusArea, false);
			}

			// ===== 2. 네 UI =====
			float baseX = inRect.x + 660f;
			float baseY = inRect.y + 60f;
			float lineHeight = 28f;

			Rect labelRect = new Rect(baseX - 170f, baseY, 120f, lineHeight);
			Widgets.Label(labelRect, "재료 탐색 방식");

			Rect radiusButtonRect = new Rect(baseX - 40f, baseY + 4f, 24f, 24f);
			if (Widgets.RadioButton(radiusButtonRect.position, data.SearchMode == IngredientSearchMode.Radius)) {
				data.SearchMode = IngredientSearchMode.Radius;
			}

			Rect radiusLabelRect = new Rect(baseX - 12f, baseY, 40f, lineHeight);
			Widgets.Label(radiusLabelRect, "반경");

			Rect storageButtonRect = new Rect(baseX + 45f, baseY + 4f, 24f, 24f);
			if (Widgets.RadioButton(storageButtonRect.position, data.SearchMode == IngredientSearchMode.Storage)) {
				data.SearchMode = IngredientSearchMode.Storage;
			}

			Rect storageLabelRect = new Rect(baseX + 73f, baseY, 60f, lineHeight);
			Widgets.Label(storageLabelRect, "저장소");

			if (data.SearchMode == IngredientSearchMode.Storage) {
				float buttonY = baseY + 34f;
				Rect storageSelectRect = new Rect(baseX - 170f, buttonY, 260f, 30f);

				string buttonLabel;
				if (data.SelectedStorageId == BillData.AllStoragesId) {
					buttonLabel = "재료 저장소: (모든 저장소)";
				} else if (!string.IsNullOrEmpty(data.SelectedZoneLabel)) {
					buttonLabel = "재료 저장소: " + data.SelectedZoneLabel;
				} else {
					buttonLabel = "재료 저장소 선택";
				}

				if (Widgets.ButtonText(storageSelectRect, buttonLabel)) {
					List<FloatMenuOption> options = new List<FloatMenuOption>();

					options.Add(new FloatMenuOption("(모든 저장소)", delegate {
						data.SelectedStorageId = BillData.AllStoragesId;
						data.SelectedZoneId = -1;
						data.SelectedZoneLabel = "(모든 저장소)";
						Log.Message("[BillIngredientSource] Selected storage: " + data.SelectedStorageId);
					}));

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
								Log.Message("[BillIngredientSource] Selected storage: " + data.SelectedStorageId);
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