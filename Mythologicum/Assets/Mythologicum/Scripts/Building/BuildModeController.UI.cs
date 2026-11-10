namespace Mythologicum
{
    public sealed partial class BuildModeController
    {
        /// <summary>Reflows the visible interface after an artist edit, without advancing the park.</summary>
        public void RefreshInterfacePresentation()
        {
            if (grid == null || buildingConstruction == null) return;
            creatureInspection?.Refresh();
            buildingInspection?.Refresh();
            visitorPresentation?.Refresh();
            enclosurePresentation?.Refresh();
            palette?.Refresh(buildingConstruction.Network.HasEntrance);
            parkStatus?.Refresh();
            management?.Refresh();
            parkManagement?.Refresh();
            notifications?.Refresh();
            encyclopedia?.Refresh();
        }
    }
}
