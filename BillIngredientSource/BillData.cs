using Verse;

namespace BillIngredientSource {
	public class BillData : IExposable {
		public IngredientSearchMode SearchMode = IngredientSearchMode.Radius;
		public string SelectedStorageId;

		public void ExposeData() {
			Scribe_Values.Look(ref SearchMode, "searchMode", IngredientSearchMode.Radius);
			Scribe_Values.Look(ref SelectedStorageId, "selectedStorageId");
		}
	}
}