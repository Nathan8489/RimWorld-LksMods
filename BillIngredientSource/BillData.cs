using Verse;

namespace BillIngredientSource {
	public class BillData : IExposable {
		public const string AllStoragesId = "__ALL_STORAGES__";

		public IngredientSearchMode SearchMode = IngredientSearchMode.Radius;

		// 선택된 저장소 식별자
		// 예: "__ALL_STORAGES__", "Zone_12", "Building_345"
		public string SelectedStorageId;

		// 기존 zone 전용 값들 (임시 호환 유지)
		public int SelectedZoneId = -1;
		public string SelectedZoneLabel;

		public void ExposeData() {
			Scribe_Values.Look(ref SearchMode, "searchMode", IngredientSearchMode.Radius);
			Scribe_Values.Look(ref SelectedStorageId, "selectedStorageId");
			Scribe_Values.Look(ref SelectedZoneId, "selectedZoneId", -1);
			Scribe_Values.Look(ref SelectedZoneLabel, "selectedZoneLabel");
		}
	}
}
