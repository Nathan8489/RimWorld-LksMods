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

			Rect vanillaRadiusArea = new Rect(inRect.x + 640f, inRect.y + 28f, 330f, 70f);

			// ===== 1. 저장소 모드일 때 바닐라 반경 UI 숨기기 =====
			if (data.SearchMode == IngredientSearchMode.Storage) {
				Widgets.DrawBoxSolid(vanillaRadiusArea, new Color(0.07f, 0.08f, 0.09f, 1f));
				Widgets.ButtonInvisible(vanillaRadiusArea, false);
			}

			// ===== 2. 커스텀 UI =====
			float baseX = vanillaRadiusArea.x + 8f;
			float baseY = vanillaRadiusArea.y + 4f;
			float lineHeight = 28f;

			Rect labelRect = new Rect(baseX - 120f, baseY, 120f, lineHeight);
			Widgets.Label(labelRect, "재료 탐색 방식");

			Rect radiusButtonRect = new Rect(baseX + 10f, baseY + 4f, 24f, 24f);
			if (Widgets.RadioButton(radiusButtonRect.position, data.SearchMode == IngredientSearchMode.Radius)) {
				data.SearchMode = IngredientSearchMode.Radius;
			}

			Rect radiusLabelRect = new Rect(baseX + 38f, baseY, 40f, lineHeight);
			Widgets.Label(radiusLabelRect, "반경");

			Rect storageButtonRect = new Rect(baseX + 95f, baseY + 4f, 24f, 24f);
			if (Widgets.RadioButton(storageButtonRect.position, data.SearchMode == IngredientSearchMode.Storage)) {
				data.SearchMode = IngredientSearchMode.Storage;
				if (string.IsNullOrEmpty(data.SelectedStorageId)) {
					data.SelectedStorageId = BillData.AllStoragesId;
					data.SelectedZoneId = -1;
					data.SelectedZoneLabel = "(모든 저장소)";
				}
			}

			Rect storageLabelRect = new Rect(baseX + 123f, baseY, 100f, lineHeight);
			Widgets.Label(storageLabelRect, "저장소");

			if (data.SearchMode == IngredientSearchMode.Storage) {
				float buttonY = baseY + 34f;
				Rect storageSelectRect = new Rect(baseX - 40f, buttonY, 280f, 30f);

				Map map = bill.Map;
				string currentStorageLabel = ResolveStorageLabel(map, data);
				string buttonLabel = string.IsNullOrEmpty(currentStorageLabel)
					? "재료 저장소 선택"
					: "재료 저장소: " + currentStorageLabel;

				if (Widgets.ButtonText(storageSelectRect, buttonLabel)) {
					List<FloatMenuOption> options = new List<FloatMenuOption>();

					options.Add(new FloatMenuOption("(모든 저장소)", delegate {
						data.SelectedStorageId = BillData.AllStoragesId;
						data.SelectedZoneId = -1;
						data.SelectedZoneLabel = "(모든 저장소)";
						Log.Message("[BillIngredientSource] Selected storage: " + data.SelectedStorageId);
					}));

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

					Find.WindowStack.Add(new FloatMenu(options));
				}
			}
		}

		private static string ResolveStorageLabel(Map map, BillData data) {
			if (data == null) {
				return null;
			}

			if (data.SelectedStorageId == BillData.AllStoragesId) {
				return "(모든 저장소)";
			}

			if (map != null && !string.IsNullOrEmpty(data.SelectedStorageId) && data.SelectedStorageId.StartsWith("Zone_")) {
				string tail = data.SelectedStorageId.Substring("Zone_".Length);
				if (int.TryParse(tail, out int zoneId)) {
					Zone_Stockpile zone = map.zoneManager.AllZones
						.OfType<Zone_Stockpile>()
						.FirstOrDefault(z => z.ID == zoneId);
					if (zone != null) {
						data.SelectedZoneId = zone.ID;
						data.SelectedZoneLabel = zone.label;
						return string.IsNullOrEmpty(zone.label) ? "(이름 없음)" : zone.label;
					}
					return "(저장소 없음)";
				}
			}

			if (!string.IsNullOrEmpty(data.SelectedZoneLabel)) {
				return data.SelectedZoneLabel;
			}

			return null;
		}
	}
}
