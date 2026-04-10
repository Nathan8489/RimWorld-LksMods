using RimWorld;
using Verse;

namespace BillIngredientSource {
	public enum SelectedStorageKind {
		None = 0,
		Zone = 1,
		SlotGroup = 2,
		StorageGroup = 3
	}

	public class BillData : IExposable {
		public IngredientSearchMode SearchMode = IngredientSearchMode.Radius;

		// 새 구조
		public bool UseAllStorages;
		public ISlotGroup SelectedStorageGroup;

		// 재매칭용 메타데이터
		public string SelectedStorageLabel;
		public SelectedStorageKind SelectedStorageKind = SelectedStorageKind.None;

		// 구버전 마이그레이션용 필드
		public string LegacySelectedStorageId;
		public int LegacySelectedZoneId = -1;
		public string LegacySelectedZoneLabel;

		public void ExposeData() {
			Scribe_Values.Look(ref SearchMode, "searchMode", IngredientSearchMode.Radius);
			Scribe_Values.Look(ref UseAllStorages, "useAllStorages", defaultValue: false);
			Scribe_Values.Look(ref SelectedStorageLabel, "selectedStorageLabel");
			Scribe_Values.Look(ref SelectedStorageKind, "selectedStorageKind", SelectedStorageKind.None);

			if (Scribe.mode == LoadSaveMode.Saving) {
				SaveSlotReferencable(SelectedStorageGroup, "selectedStorageGroup");
			} else if (Scribe.mode == LoadSaveMode.LoadingVars || Scribe.mode == LoadSaveMode.ResolvingCrossRefs) {
				LoadSlotReferencable(ref SelectedStorageGroup, "selectedStorageGroup");
			}

			// 레거시 로드용
			Scribe_Values.Look(ref LegacySelectedStorageId, "selectedStorageId");
			Scribe_Values.Look(ref LegacySelectedZoneId, "selectedZoneId", -1);
			Scribe_Values.Look(ref LegacySelectedZoneLabel, "selectedZoneLabel");
		}

		private static void SaveSlotReferencable(ISlotGroup slot, string key) {
			ILoadReferenceable refee = null;

			ILoadReferenceable loadReferenceable = slot as ILoadReferenceable;
			if (loadReferenceable != null) {
				refee = loadReferenceable;
			} else {
				SlotGroup slotGroup = slot as SlotGroup;
				if (slotGroup != null) {
					ILoadReferenceable parent = slotGroup.parent as ILoadReferenceable;
					if (parent != null) {
						refee = parent;
					}
				}
			}

			Scribe_References.Look(ref refee, key);
		}

		private static void LoadSlotReferencable(ref ISlotGroup slot, string key) {
			ILoadReferenceable refee = null;
			Scribe_References.Look(ref refee, key);

			if (refee is ISlotGroup slotGroup) {
				slot = slotGroup;
			} else if (refee is ISlotGroupParent slotGroupParent) {
				slot = slotGroupParent.GetSlotGroup();
			} else {
				slot = null;
			}
		}

		public bool HasLegacyData() {
			return !string.IsNullOrEmpty(LegacySelectedStorageId)
				|| LegacySelectedZoneId != -1
				|| !string.IsNullOrEmpty(LegacySelectedZoneLabel);
		}

		public void ClearLegacyData() {
			LegacySelectedStorageId = null;
			LegacySelectedZoneId = -1;
			LegacySelectedZoneLabel = null;
		}

		public void ClearSelectedStorage() {
			UseAllStorages = false;
			SelectedStorageGroup = null;
			SelectedStorageLabel = null;
			SelectedStorageKind = SelectedStorageKind.None;
			SearchMode = IngredientSearchMode.Radius;
		}
	}
}