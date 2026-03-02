namespace Calloatti.Grid
{
  public partial class GridService
  {
    // STATE TRACKER: 0 = Off, 1 = Both, 2 = Terrain Only, 3 = Buildings Only
    private int _gridState = 0;

    public void ToggleTerrainGrid()
    {
      _gridState = (_gridState + 1) % 4;

      if (_gridState == 0)
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