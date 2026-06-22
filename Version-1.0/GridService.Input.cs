namespace Calloatti.Grid
{
  public partial class GridService
  {
    // STATE TRACKER
    private bool _showTerrain = false;
    private bool _showBuilding = false;

    public void ToggleTerrainGrid()
    {
      _showTerrain = !_showTerrain;
      UpdateGridState();
    }

    public void ToggleBuildingGrid()
    {
      _showBuilding = !_showBuilding;
      UpdateGridState();
    }

    private void UpdateGridState()
    {
      if (!_showTerrain && !_showBuilding)
      {
        TurnOffTerrainGrid();
        return;
      }

      if (_terrainGridRoot == null)
      {
        GenerateFullTerrainGrid();
      }
      else
      {
        _terrainGridRoot.SetActive(true);
        ProcessDirtyLevels();
        UpdateVisibleLevels();
      }
    }
  }
}