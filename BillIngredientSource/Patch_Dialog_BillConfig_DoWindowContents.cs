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
			if (!data.UseAllStorages && data.SelectedStorageGroup == null && data.HasLegacyData()) {
				LegacyStorageMigration.TryMigrate(bill, data);
			}
			bool useStorage = data.UseAllStorages || data.SelectedStorageGroup != null;

			data.SearchMode = useStorage
				? IngredientSearchMode.Storage
				: IngredientSearchMode.Radius;

			Map map = GetBillMap(bill);
			bool hasIngredientFilter = HasIngredientFilterPanel(bill);

			Rect vanillaRadiusRect = GetVanillaRadiusRect(inRect, hasIngredientFilter);
			Rect storageButtonRect = GetStorageButtonRect(vanillaRadiusRect, hasIngredientFilter);

			// 버튼 클릭은 항상 처리
			HandleStorageButtonClick(storageButtonRect, bill, data, map);

			// 저장소 사용 중일 때만 반경 차단/0처리
			if (!useStorage) {
				return;
			}

			__state = bill.ingredientSearchRadius;
			bill.ingredientSearchRadius = 0f;

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
			if (!data.UseAllStorages && data.SelectedStorageGroup == null && data.HasLegacyData()) {
				LegacyStorageMigration.TryMigrate(bill, data);
			}
			Map map = GetBillMap(bill);

			if (!data.UseAllStorages && data.SelectedStorageGroup != null) {
				StorageIngredientSource.ValidateSelectedStorage(map, bill, data);
			}

			bool hasIngredientFilter = HasIngredientFilterPanel(bill);
			bool useStorage = data.UseAllStorages || data.SelectedStorageGroup != null;

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

		private static void DrawStorageButton(Rect rect, BillData data, Map map) {
			string buttonLabel;

			if (data.UseAllStorages) {
				buttonLabel = Tr("BIS_AllStorages");
			} else {
				string selectedLabel = StorageIngredientSource.GetStorageLabel(map, data.SelectedStorageGroup);
				buttonLabel = string.IsNullOrEmpty(selectedLabel)
					? Tr("BIS_NoStorageSelected")
					: Tr("BIS_OnlyIncluded", selectedLabel);
			}

			Widgets.ButtonText(rect, buttonLabel);
		}

		private static List<FloatMenuOption> BuildStorageOptions(Bill_Production bill, BillData data, Map map) {
			List<FloatMenuOption> options = new List<FloatMenuOption>();

			options.Add(new FloatMenuOption(Tr("BIS_NoStorageSelected"), delegate {
				data.ClearSelectedStorage();
			}));

			options.Add(new FloatMenuOption(Tr("BIS_AllStorages"), delegate {
				data.UseAllStorages = true;
				data.SelectedStorageGroup = null;
				data.SelectedStorageLabel = null;
				data.SelectedStorageKind = SelectedStorageKind.None;
				data.SearchMode = IngredientSearchMode.Storage;
#if DEBUG
			if (Prefs.DevMode)
				Log.Message("[BillIngredientSource] Selected storage: ALL");
#endif
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

					string optionLabel = Tr("BIS_ZonePrefix", localZone.label);

					options.Add(new FloatMenuOption(optionLabel, delegate {
						StorageIngredientSource.AssignSelectedStorage(data, localZone.GetSlotGroup());
#if DEBUG
						if (Prefs.DevMode)
							Log.Message("[BillIngredientSource] Selected zone: " + localZone.label);
#endif
					}));
				}

				foreach (ISlotGroup group in StorageIngredientSource.GetAllSelectableStorageGroups(map)) {
					ISlotGroup localGroup = group;
					string storageLabel = SlotGroup.GetGroupLabel(localGroup);

					if (string.IsNullOrWhiteSpace(storageLabel)) {
						continue;
					}

					bool compatible = StorageIngredientSource.IsStorageCompatibleWithBill(bill, map, localGroup);
					string baseLabel = Tr("BIS_StoragePrefix", storageLabel);
					string optionLabel = compatible
						? baseLabel
						: Tr("BIS_Incompatible", baseLabel);

					options.Add(new FloatMenuOption(optionLabel, compatible ? (Action)delegate {
						StorageIngredientSource.AssignSelectedStorage(data, localGroup);
#if DEBUG
						if (Prefs.DevMode)
							Log.Message("[BillIngredientSource] Selected storage: " + SlotGroup.GetGroupLabel(localGroup));
#endif
					}
					: null));
				}
			}

			if (options.Count == 0) {
				options.Add(new FloatMenuOption(Tr("BIS_NoSelectableStorage"), null));
			}

			return options;
		}

		private static void DrawAndBlockVanillaRadiusArea(Rect vanillaRadiusRect, Rect storageButtonRect) {
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

		private static Rect GetVanillaRadiusRect(Rect inRect, bool hasIngredientFilter) {
			if (hasIngredientFilter) {
				return new Rect(inRect.xMax - BISUI.RadiusAreaRightOffset, inRect.yMax - BISUI.HasFilterBottomOffset, BISUI.RadiusAreaWidth, BISUI.RadiusAreaHeight);
			}

			return new Rect(inRect.xMax - BISUI.RadiusAreaRightOffset, inRect.y + BISUI.NoFilterY, BISUI.RadiusAreaWidth, BISUI.RadiusAreaHeight);
		}

		private static Rect GetStorageButtonRect(Rect vanillaRadiusRect, bool hasIngredientFilter) {
			return new Rect(vanillaRadiusRect.x + BISUI.ButtonInsetX, vanillaRadiusRect.y + BISUI.ButtonInsetY, vanillaRadiusRect.width - BISUI.ButtonInsetWidth, vanillaRadiusRect.height - BISUI.ButtonInsetHeight);
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

		private static string Tr(string key)
			=> key.Translate().ToString();

		private static string Tr(string key, params object[] args)
			=> string.Format(key.Translate().ToString(), args);
	}
}
