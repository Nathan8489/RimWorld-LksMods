using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace BillIngredientSource {
	[HarmonyPatch(typeof(Dialog_BillConfig), "DoWindowContents")]
	public static class Patch_Dialog_BillConfig_DoWindowContents {
		public static void Prefix(Dialog_BillConfig __instance, ref float __state) {
			Bill_Production bill = Traverse.Create(__instance).Field("bill").GetValue<Bill_Production>();
			__state = -1f;

			if (bill == null) return;

			BillData data = BillDataStore.GetOrCreate(bill);
			if (data.SearchMode != IngredientSearchMode.Storage) {
				return;
			}

			// 바닐라 반경 링/반경 UI의 실질적 동작을 죽이기 위해 잠시 0으로 바꿈
			__state = bill.ingredientSearchRadius;
			bill.ingredientSearchRadius = 0f;
		}

		public static void Postfix(Dialog_BillConfig __instance, Rect inRect, float __state) {
			Bill_Production bill = Traverse.Create(__instance).Field("bill").GetValue<Bill_Production>();
			if (bill == null) return;

			// Prefix에서 바꿨던 반경 복구
			if (__state >= 0f) {
				bill.ingredientSearchRadius = __state;
			}

			BillData data = BillDataStore.GetOrCreate(bill);
			Map map = GetBillMap(bill);

			bool hasIngredientFilter = HasIngredientFilterPanel(bill);

			Rect modeRowRect = GetModeRowRect(inRect, hasIngredientFilter);
			Rect vanillaRadiusRect = GetVanillaRadiusRect(inRect, hasIngredientFilter);
			Rect storageButtonRect = GetStorageButtonRect(vanillaRadiusRect);

			// 저장소 모드일 때:
			// 1) 드래그/스크롤/클릭 이벤트 먹기
			// 2) 그 자리에 저장소 버튼 배치
			// 3) 바닐라 반경 라벨/슬라이더 영역 가리기
			DrawModeRow(modeRowRect, data);

			if (data.SearchMode == IngredientSearchMode.Storage) {
				DrawStorageButton(storageButtonRect, data, map);
				DrawAndBlockVanillaRadiusArea(vanillaRadiusRect, storageButtonRect);
			}
		}

		private static void DrawModeRow(Rect rowRect, BillData data) {
			float labelWidth = 120f;
			float radioSize = 24f;
			float gap = 8f;

			Rect labelRect = new Rect(rowRect.x, rowRect.y, labelWidth, rowRect.height);
			Widgets.Label(labelRect, "재료 탐색 방식");

			float x = labelRect.xMax + 6f;

			Rect radiusButtonRect = new Rect(x, rowRect.y + 3f, radioSize, radioSize);
			bool radiusSelected = data.SearchMode == IngredientSearchMode.Radius;
			if (Widgets.RadioButton(radiusButtonRect.position, radiusSelected)) {
				data.SearchMode = IngredientSearchMode.Radius;
			}

			Rect radiusLabelRect = new Rect(radiusButtonRect.xMax + 4f, rowRect.y, 34f, rowRect.height);
			Widgets.Label(radiusLabelRect, "반경");

			x = radiusLabelRect.xMax + gap;

			Rect storageButtonRect = new Rect(x, rowRect.y + 3f, radioSize, radioSize);
			bool storageSelected = data.SearchMode == IngredientSearchMode.Storage;
			if (Widgets.RadioButton(storageButtonRect.position, storageSelected)) {
				data.SearchMode = IngredientSearchMode.Storage;
			}

			Rect storageLabelRect = new Rect(storageButtonRect.xMax + 4f, rowRect.y, 42f, rowRect.height);
			Widgets.Label(storageLabelRect, "저장소");
		}

		private static void DrawStorageButton(Rect rect, BillData data, Map map) {
			string selectedLabel = StorageIngredientSource.GetStorageLabel(map, data.SelectedStorageId, data.SelectedZoneLabel);
			string buttonLabel = string.IsNullOrEmpty(selectedLabel)
				? "재료 저장소 선택"
				: "재료 저장소: " + selectedLabel;

			if (Widgets.ButtonText(rect, buttonLabel)) {
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

						if (string.IsNullOrWhiteSpace(localZone.label)) {
							continue;
						}

						string optionLabel = "[구역] " + localZone.label;

						options.Add(new FloatMenuOption(optionLabel, delegate {
							data.SelectedZoneId = localZone.ID;
							data.SelectedZoneLabel = localZone.label;
							data.SelectedStorageId = "Zone_" + localZone.ID;
							Log.Message("[BillIngredientSource] Selected storage: " + data.SelectedStorageId);
						}));
					}

					foreach (SlotGroup slotGroup in StorageIngredientSource.GetAllStorageSlotGroups(map)) {
						SlotGroup localSlotGroup = slotGroup;
						string storageLabel = StorageIngredientSource.GetSlotGroupLabel(localSlotGroup);

						if (string.IsNullOrWhiteSpace(storageLabel)) {
							continue;
						}

						string optionLabel = "[저장소] " + storageLabel;

						options.Add(new FloatMenuOption(optionLabel, delegate {
							data.SelectedZoneId = -1;
							data.SelectedZoneLabel = storageLabel;
							data.SelectedStorageId = StorageIngredientSource.GetSlotGroupStorageId(localSlotGroup);
							Log.Message("[BillIngredientSource] Selected storage: " + data.SelectedStorageId);
						}));
					}
				}

				if (options.Count == 0) {
					options.Add(new FloatMenuOption("(선택 가능한 저장소 없음)", null));
				}

				Find.WindowStack.Add(new FloatMenu(options));
			}
		}

		private static void DrawAndBlockVanillaRadiusArea(Rect vanillaRadiusRect, Rect storageButtonRect) {
			// 버튼 위/아래를 나눠서 그려 버튼은 안 덮음
			Rect topRect = new Rect(
				vanillaRadiusRect.x,
				vanillaRadiusRect.y,
				vanillaRadiusRect.width,
				Mathf.Max(0f, storageButtonRect.y - vanillaRadiusRect.y)
			);

			Rect leftRect = new Rect(
				vanillaRadiusRect.x,
				storageButtonRect.y,
				Mathf.Max(0f, storageButtonRect.x - vanillaRadiusRect.x),
				storageButtonRect.height
			);

			Rect rightRect = new Rect(
				storageButtonRect.xMax,
				storageButtonRect.y,
				Mathf.Max(0f, vanillaRadiusRect.xMax - storageButtonRect.xMax),
				storageButtonRect.height
			);

			Rect bottomRect = new Rect(
				vanillaRadiusRect.x,
				storageButtonRect.yMax,
				vanillaRadiusRect.width,
				Mathf.Max(0f, vanillaRadiusRect.yMax - storageButtonRect.yMax)
			);

			Color bg = new Color(0.07f, 0.08f, 0.09f, 1f);

			if (topRect.height > 0f) Widgets.DrawBoxSolid(topRect, bg);
			if (leftRect.width > 0f) Widgets.DrawBoxSolid(leftRect, bg);
			if (rightRect.width > 0f) Widgets.DrawBoxSolid(rightRect, bg);
			if (bottomRect.height > 0f) Widgets.DrawBoxSolid(bottomRect, bg);

			Event e = Event.current;
			if (e == null) return;

			if (!vanillaRadiusRect.Contains(e.mousePosition)) {
				return;
			}

			// 버튼 영역은 막지 않음
			if (storageButtonRect.Contains(e.mousePosition)) {
				return;
			}

			switch (e.type) {
			case EventType.MouseDown:
			case EventType.MouseUp:
			case EventType.MouseDrag:
			case EventType.ScrollWheel:
			case EventType.DragUpdated:
			case EventType.DragPerform:
				e.Use();
				break;
			}
		}

		private static Rect GetModeRowRect(Rect inRect, bool hasIngredientFilter) {
			if (hasIngredientFilter) {
				// 재료 필터가 있는 경우:
				// 우측 재료 설정 열의 마지막 줄 위치
				return new Rect(inRect.xMax - 290f, inRect.yMax - 118f, 270f, 30f);
			}

			// 재료 필터가 없는 경우:
			// 우측 상단의 재료 탐색 범위 아래
			return new Rect(inRect.xMax - 290f, inRect.y + 58f, 270f, 30f);
		}

		private static Rect GetVanillaRadiusRect(Rect inRect, bool hasIngredientFilter) {
			if (hasIngredientFilter) {
				// 하단의 "재료 탐색 범위: ..." + 슬라이더 줄
				return new Rect(inRect.xMax - 323f, inRect.yMax - 67f, 306f, 46f);
			}

			// 우측 상단의 반경 라벨/슬라이더 영역
			return new Rect(inRect.xMax - 325f, inRect.y + 18f, 310f, 78f);
		}

		private static Rect GetStorageButtonRect(Rect vanillaRadiusRect) {
			return new Rect(vanillaRadiusRect.x + 2f, vanillaRadiusRect.y + 2f, vanillaRadiusRect.width - 4f, 28f);
		}

		private static Map GetBillMap(Bill_Production bill) {
			if (bill?.billStack?.billGiver is Thing thing && thing.Map != null) {
				return thing.Map;
			}
			return Find.CurrentMap;
		}

		private static bool HasIngredientFilterPanel(Bill_Production bill) {
			if (bill?.recipe?.ingredients == null) {
				return false;
			}

			for (int i = 0; i < bill.recipe.ingredients.Count; i++) {
				ThingFilter filter = bill.recipe.ingredients[i].filter;
				if (filter != null && filter.AllowedDefCount > 1) {
					return true;
				}
			}

			return false;
		}
	}
}