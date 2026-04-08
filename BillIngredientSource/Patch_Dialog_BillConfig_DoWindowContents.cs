using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace BillIngredientSource {
	[HarmonyPatch(typeof(Dialog_BillConfig), "DoWindowContents")]
	public static class Patch_Dialog_BillConfig_DoWindowContents {
		public static void Prefix(Dialog_BillConfig __instance, Rect inRect, ref float __state) {
			Bill_Production bill = Traverse.Create(__instance).Field("bill").GetValue<Bill_Production>();
			__state = -1f;

			if (bill == null) return;

			BillData data = BillDataStore.GetOrCreate(bill);
			bool useStorage = !string.IsNullOrEmpty(data.SelectedStorageId);

			data.SearchMode = useStorage
				? IngredientSearchMode.Storage
				: IngredientSearchMode.Radius;

			if (!useStorage) {
				return;
			}

			__state = bill.ingredientSearchRadius;
			bill.ingredientSearchRadius = 0f;

			Map map = GetBillMap(bill);
			bool hasIngredientFilter = HasIngredientFilterPanel(bill);

			Rect vanillaRadiusRect = GetVanillaRadiusRect(inRect, hasIngredientFilter);
			Rect storageButtonRect = GetStorageButtonRect(vanillaRadiusRect, hasIngredientFilter);

			HandleStorageButtonClick(storageButtonRect, bill, data, map);
			BlockVanillaRadiusInput(vanillaRadiusRect, storageButtonRect);
		}

		private static void HandleStorageButtonClick(Rect rect, Bill_Production bill, BillData data, Map map) {
			Event e = Event.current;
			if (e == null) return;

			if (e.type != EventType.MouseDown || e.button != 0) {
				return;
			}

			if (!rect.Contains(e.mousePosition)) {
				return;
			}

			List<FloatMenuOption> options = BuildStorageOptions(bill, data, map);
			Find.WindowStack.Add(new FloatMenu(options));
			e.Use();
		}

		private static void BlockVanillaRadiusInput(Rect vanillaRadiusRect, Rect storageButtonRect) {
			Event e = Event.current;
			if (e == null) return;

			if (!vanillaRadiusRect.Contains(e.mousePosition)) {
				return;
			}

			// 버튼 자체 클릭은 HandleStorageButtonClick에서 처리
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

		public static void Postfix(Dialog_BillConfig __instance, Rect inRect, float __state) {
			Bill_Production bill = Traverse.Create(__instance).Field("bill").GetValue<Bill_Production>();
			if (bill == null) return;

			if (__state >= 0f) {
				bill.ingredientSearchRadius = __state;
			}

			BillData data = BillDataStore.GetOrCreate(bill);
			Map map = GetBillMap(bill);

			bool hasIngredientFilter = HasIngredientFilterPanel(bill);
			bool useStorage = !string.IsNullOrEmpty(data.SelectedStorageId);

			data.SearchMode = useStorage
				? IngredientSearchMode.Storage
				: IngredientSearchMode.Radius;

			Rect vanillaRadiusRect = GetVanillaRadiusRect(inRect, hasIngredientFilter);
			Rect storageButtonRect = GetStorageButtonRect(vanillaRadiusRect, hasIngredientFilter);

			DrawStorageButton(storageButtonRect, data, map);

			if (useStorage) {
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
				? "저장구역 선택안함(바닐라)"
				: selectedLabel + "만 포함";

			Widgets.ButtonText(rect, buttonLabel);
		}

		private static List<FloatMenuOption> BuildStorageOptions(Bill_Production bill, BillData data, Map map) {
			List<FloatMenuOption> options = new List<FloatMenuOption>();

			options.Add(new FloatMenuOption("저장구역 선택안함(바닐라)", delegate {
				data.SelectedStorageId = null;
				data.SelectedZoneId = -1;
				data.SelectedZoneLabel = null;
				data.SearchMode = IngredientSearchMode.Radius;
			}));

			options.Add(new FloatMenuOption("모든 저장구역 포함", delegate {
				data.SelectedStorageId = BillData.AllStoragesId;
				data.SelectedZoneId = -1;
				data.SelectedZoneLabel = "모든 저장구역";
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

					bool compatible = StorageIngredientSource.IsStorageCompatibleWithBill(bill, localSlotGroup);
					string optionLabel = compatible
						? "[저장소] " + storageLabel
						: "[저장소] " + storageLabel + " (호환되지 않음)";

					options.Add(new FloatMenuOption(optionLabel, compatible ? (Action)delegate {
						data.SelectedZoneId = -1;
						data.SelectedZoneLabel = storageLabel;
						data.SelectedStorageId = StorageIngredientSource.GetSlotGroupStorageId(localSlotGroup);
						Log.Message("[BillIngredientSource] Selected storage: " + data.SelectedStorageId);
					}
					: null));
				}
			}

			if (options.Count == 0) {
				options.Add(new FloatMenuOption("(선택 가능한 저장소 없음)", null));
			}

			return options;
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
				// 하단 "재료 탐색 범위" 라벨 + 슬라이더 영역
				return new Rect(inRect.xMax - 300f, inRect.yMax - 20f, 280f, 60f);
			}

			// 우측 상단 반경 영역
			return new Rect(inRect.xMax - 300f, inRect.y + 90f, 280f, 60f);
		}

		private static Rect GetStorageButtonRect(Rect vanillaRadiusRect, bool hasIngredientFilter) {
			return new Rect(vanillaRadiusRect.x + 6f, vanillaRadiusRect.y + 4f, vanillaRadiusRect.width - 12f, vanillaRadiusRect.height - 8f);
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