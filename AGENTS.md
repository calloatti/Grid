Include ..\AGENTS.md

# Grid Mod — Timberborn

## Project Overview

A Timberborn mod that adds grid overlays, water planning tools, markers, and rulers.  
**Mod ID:** `Calloatti.Grid` · **Assembly:** `grid` · **Namespace:** `Calloatti.Grid`

## File Map

| File | Purpose |
|------|---------|
| `Source/ModStarter.cs` | Entry point — implements `IModStarter`, initializes `SimpleConfig` |
| `Source/GridModule.cs` | Grid overlay: configurator, input service (hotkeys), serializable settings |
| `Source/GridService.cs` | Core grid rendering — mesh-based 2D grid for terrain & buildings, 3D caches |
| `Source/TopoModule.cs` | Topo overlay configurator & input service |
| `Source/TopoService.cs` | Height-map visualization using sprite atlas + chunked meshes |
| `Source/WaterModule.cs` | Water planner configurator & events |
| `Source/WaterService.cs` | Water planned areas, moisture spread simulation (BFS), visualizers |
| `Source/MarkerModule.cs` | Marker configurator, input service, settings |
| `Source/MarkerService.cs` | Colored cross markers on columns, save/load persistence |
| `Source/RulerModule.cs` | Ruler configurator & input service |
| `Source/RulerService.cs` | Ruler segments, overlap management, sprite-based display |
| `Source/BottomBarConfigurator.cs` | Registers `BottomBarButtonGroup` as `BottomBarModule` provider |
| `Source/BottomBarButtonGroup.cs` | Tool group UI — Marker, Ruler, Water tools via `ToolButtonFactory` |
| `Source/WaterTool*.cs` | Water tools (Planner, Eraser, Rise, Lower, DeleteAll) |
| `Source/RulerTool*.cs` | Ruler tools (Draw, DeleteAll) |
| `Source/MarkerTool*.cs` | Marker tools (Place/cycle color, DeleteAll) |
| `Source/CGModule.cs` | Construction guidelines configurator (DI) |
| `Source/CGService.cs` | Construction guidelines number overlay — renders distance numbers via pooled quads + atlas UV |
| `Source/CGPatches.cs` | Harmony postfixes on `AddCoordinatesToGuidelines` + `CrossParameters.Reset()` |
| `simpleconfig.txt` | Config schema (Grid settings: offsets, colors, highlights) |
| `manifest.json` | Mod manifest (id, version, game deps) |
| `Grid.csproj` | SDK-style project, imports `CommonModSettings.props` |

## Architecture Patterns

### Module/Configurator + Service + Tool
Each feature follows this pattern:
1. **Module file** — `XxxConfigurator` (`[Context("Game")]`, extends `Configurator`) + `XxxInputService` (hotkeys) + optional settings/serializable class
2. **Service file** — Core logic, implements relevant singleton interfaces
3. **Tool files** — `ITool`, `IToolDescriptor`, `IInputProcessor`, `ILoadableSingleton`

### Key Interfaces
- `ILoadableSingleton` — `Load()` on init
- `IPostLoadableSingleton` — `PostLoad()` after all singletons
- `ILateUpdatableSingleton` — `LateUpdateSingleton()` per frame
- `ISaveableSingleton` — `Save()` for persistence
- `IDisposable` — cleanup
- `IInputProcessor` — keybinding/hotkey handling

### Registration
1. Configurator binds services/tools via `Bindito.Core`
2. Bottom bar tools registered in `BottomBarButtonGroup.GetElements()` via `AddToolButton()`
3. Keybindings in `KeyBindings/*.blueprint.json`, grouped in `KeyBindingGroups/`
4. Tool group defined in `ToolGroups/ToolGroups.Markers.blueprint.json`
5. Localization strings in `Localizations/*.csv`

## Grid Rendering (GridService)

- Mesh-based rendering using `MeshFilter`/`MeshRenderer` with `MeshTopology.Lines`
- Two 3D boolean caches: `_isTerrainCache[,,]` and `_isBuildingCache[,,]`
- Separate meshes per Z-level: terrain surface/slice, building surface/slice (each + highlight variant)
- Bedrock mesh at z=0
- Materials use `Hidden/Internal-Colored` shader
- `GetOffsetVertex()` adjusts vertex positions based on neighbor solidity (for visual offset)
- Highlight intervals for city planning guides
- Reactive: listens to `BlockObjectSetEvent`/`UnsetEvent` and terrain height changes, marks dirty levels, rebuilds on cooldown (0.25s)

## Block Occupation Detection

`CheckIfBuildingBlock(pos)` in `GridService.cs:305`:
- Queries `_blockService.GetObjectsAt(pos)`
- Returns true if any `BlockObject` at the position has a non-`Path` block occupation
- This catches all placed objects: buildings, ruins, relics, map editor objects, trees, bushes, crops, etc.
- Only excludes `BlockOccupations.Path` (paths are exclusive occupants)

## BlockOccupations Enum (Flags)

`Timberborn.BlockSystem.BlockOccupations` — a `[Flags]` enum defining which sub-parts of a block are occupied:

| Value | Name | Description |
|-------|------|-------------|
| `0` | `None` | Empty/unoccupied |
| `1` | `Floor` | Objects ON the floor surface (decorations, small items); clickable, not an obstacle |
| `2` | `Bottom` | Bottom volume portion of structures; used for navigation/pathfinding |
| `4` | `Top` | Top portion of structures |
| `8` | `Corners` | Corner occupation |
| `0x10` | `Path` | Walkable path surface; clickable |
| `0x20` | `Middle` | Middle portion (used by mechanical systems: `Bottom \| Middle`) |
| `-1` | `All` | Entire block occupied |

- `Block.Occupation` gives the occupation for a specific `BlockObject` at a position
- `WorldBlock` stores separate `BlockObject` references per slot (Floor, Bottom, Top, Corners, Path, Middle, Underground)
- Multiple flags can be combined on a single `Block` (bitmask)
- Rendering priority (highest to lowest): Top > Corners > Middle > Bottom > Path > Floor

## MapEditor Context & Dual Persistence Keys

### Context Registration
All module configurators now register in both `[Context("Game")]` and `[Context("MapEditor")]` to make tools available in the map editor toolbar:

| Configurator | File |
|---|---|
| `GridConfigurator` | `Source/GridModule.cs` |
| `TopoConfigurator` | `Source/TopoModule.cs` |
| `MarkerConfigurator` | `Source/MarkerModule.cs` |
| `RulerConfigurator` | `Source/RulerModule.cs` |
| `BottomBarConfigurator` | `Source/BottomBarConfigurator.cs` |
| `MapEditorBottomBarConfigurator` | `Source/MapEditorBottomBarConfigurator.cs` |

### Dual Persistence Keys (No Cross-Context Leak)
To prevent map editor data from transferring into new games, each persistable service uses separate singleton keys per context:

| Service | Game Key | MapEditor Key |
|---|---|---|
| `MarkerService` | `Calloatti.Grid.Markers` | `Calloatti.Grid.Markers.Map` |
| `RulerService` | `Calloatti.Grid.Rulers` | `Calloatti.Grid.Rulers.Map` |

Key selection uses `MapEditorMode.IsMapEditor` (from `Timberborn.MapStateSystem`):
- `Save()` writes to the context-appropriate key
- `Load()` reads from the context-appropriate key

This ensures:
- Map makers' markers/rulers persist across MapEditor sessions ✓
- Markers/rulers placed in MapEditor do NOT appear when a player starts a new game from that map ✓
- Game save/load works as before ✓

## Coordinate System

### Grid ↔ World Mapping (`CoordinateSystem`)
```csharp
// Grid (x, y, z) → World (x, z, y)
GridToWorld(Vector3Int c) => new Vector3(c.x, c.z, c.y);
WorldToGrid(Vector3 p)    => new Vector3(p.x, p.z, p.y);
```

| Grid Component | Meaning | Maps To World |
|---|---|---|
| `.x` | East-West | `World.x` |
| `.y` | North-South | `World.z` |
| `.z` | Height (vertical) | `World.y` |

- `Vector3Int` components: `.x` = east-west, `.y` = north-south, `.z` = height
- `CrossParameters.Center` is `Vector3Int(x, y, z)` → same mapping
- To extract grid coords from a tile matrix world position: `gx = (int)worldPos.x`, `gy = (int)worldPos.z` (not `.y`!)

## Construction Guidelines (Game Feature)

### Source
`ConstructionGuidelinesRenderingService` in `Timberborn.ConstructionGuidelines` (public class).

### How It Works
- Renders colored tile squares in a cross pattern from the cursor when the `ShowGuidelines` key is held or guidelines are toggled on.
- Also shown during building placement (via `ConstructionModeGuidelinesShower` → `ConstructionGuidelinesToggle`).
- Radius is 30 (from `ConstructionGuidelinesSpec`).

### Key Methods
| Method | Role |
|---|---|
| `AddCoordinatesToGuidelines(Vector3 center, IEnumerable<Vector2Int> coords)` | Clears and populates `_tilesAtSameLevel`, `_tilesBelow`, `_tilesAbove` (all `List<Matrix4x4>`) |
| `GetGuidelinesCoordinates(Vector3 center, Vector2Int min, Vector2Int max)` | Yields cross-arm tile coords, excluding the footprint area (`x < min.x \|\| x > max.x` for horizontal, analogous for vertical) |
| `GetTilesInsideFootprint(Vector2Int min, Vector2Int max, ...)` | Yields coords inside the bounding box that are NOT occupied by the building (empty spaces in irregular footprints) |
| `SetPreviewFootprint(...)` / `UpdateBlockObjectPreviewTiles(...)` | Called when a building is selected; provides footprint min/max |
| `GetGuidelinesFromMousePosition()` | No-building case; adds center tile separately via `_tilesAtSameLevel.Add(CreateMatrix(Center, ...))` AFTER `AddCoordinatesToGuidelines` |

### Tile Lists
- `_tilesAtSameLevel` — tiles at same elevation as cursor
- `_tilesBelow` — tiles below cursor elevation
- `_tilesAbove` — tiles above cursor elevation
- These are visual categories only; for numbering/overlay purposes, treat as one combined list.
- Building footprint tiles are **NOT** in the lists (only cross-arm tiles are).
- Center tile (cursor position) is added separately after `AddCoordinatesToGuidelines` returns.

### Tile Matrix Positions
`CreateMatrix(Vector3Int coordinates, float markerYOffset)`:
```
Matrix4x4.TRS(GridToWorld(coordinates) + new Vector3(0.5f, markerYOffset, 0.5f), Quaternion.identity, Vector3.one)
```
Extract world position: `matrix.GetColumn(3)` → Vector3 (world x, world y/height, world z/NS).

### Cross Pattern Structure (No Building)
When no building is selected: `min == max == center.XY()` (using 2D grid coords).
- **Horizontal arm** (West/East): tiles at `(x, center.y)` for `x` in `[center.x - radius, center.x + radius]`, excluding center x
- **Vertical arm** (South/North): tiles at `(center.x, y)` for `y` in `[center.y - radius, center.y + radius]`, excluding center y
- Center tile added separately (not in `AddCoordinatesToGuidelines` output).

### Cross Pattern Structure (Building Selected)
When a building is selected: `min/max` define the footprint bounding box.
- **Horizontal arm**: tiles at `(x, y)` for x outside footprint, y within footprint y-range → width of arm matches footprint height
- **Vertical arm**: tiles at `(x, y)` for y outside footprint, x within footprint x-range → height of arm matches footprint width
- Footprint tiles are excluded (only empty spaces within the bounding box are included via `GetTilesInsideFootprint`).
- Distance from center = `max(|gridX - centerX|, |gridY - centerY|)` — but since tiles are on cardinal axes only, one axis always equals center, so distance = axis difference.

### Cardinal Direction Classification
For numbering, classify tiles by which arm they're on:
- **West**: `gridY == centerY && gridX < centerX`
- **East**: `gridY == centerY && gridX > centerX`
- **South**: `gridX == centerX && gridY < centerY`
- **North**: `gridX == centerX && gridY > centerY`
- **Center**: `gridX == centerX && gridY == centerY` (distance 0, skip for numbering)

### Harmony Patching
- `CGPatches` postfixes `AddCoordinatesToGuidelines` to capture tile data and populate dot positions.
- `CGClearPatch` postfixes `CrossParameters.Reset()` to clear dot positions when guidelines hide.
- `Timberborn.ConstructionGuidelines` must be publicized in `Grid.csproj` (for accessing `CrossParameters` type).
- `Harmony.PatchAll()` is called in `ModStarter.StartMod()`.
- Private fields accessed via Harmony `____` parameter injection: `____tilesAtSameLevel`, `____tilesBelow`, `____tilesAbove`, `____lastCrossParameters`.

### CrossParameters
State holder for the cross pattern, stored in `_lastCrossParameters` on `ConstructionGuidelinesRenderingService`.

| Property | Type | Description |
|---|---|---|
| `Center` | `Vector3Int` | Cursor/grid center (grid x, y, z) |
| `Min` | `Vector2Int` | Footprint bounding box min (2D grid) |
| `Max` | `Vector2Int` | Footprint bounding box max (2D grid) |

- **No building**: `Min == Max == Center.XY()` (center's x,y as Vector2Int)
- **Building selected**: `Min`/`Max` define the footprint bounding box
- `CrossParametersUpdated()` — dirty check, returns `true` only if values actually changed (triggers tile rebuild)
- `Reset()` — blanks to `-1,-1,-1` when guidelines are hidden; we postfix this to clear dot positions
- For distance numbering: use `Min`/`Max` to measure distance from footprint **edge**, not from center

## Key Game API Namespaces
- `Timberborn.BlockSystem` — `BlockObject`, `IBlockService`, `BlockOccupations`, events
- `Timberborn.Buildings` — `Building` component
- `Timberborn.ConstructionGuidelines` — `ConstructionGuidelinesRenderingService`, `CrossParameters`, `ConstructionGuidelinesSpec`
- `Timberborn.Coordinates` — `CoordinateSystem` (GridToWorld/WorldToGrid)
- `Timberborn.TerrainSystem` — `ITerrainService`
- `Timberborn.SingletonSystem` — `EventBus`
- `Timberborn.ToolSystem` — `ITool`, `ToolService`
- `Timberborn.Persistence` — `ISaveableSingleton`
- `Bindito.Core` — DI

## Build & Run
- SDK-style project: `Grid.csproj` (netstandard2.1 assumed)
- References via `CommonModSettings.props` (game assemblies)
- No test framework detected
- Run by placing build output in Timberborn's mods folder

## Conventions
- **Namespaces**: `Calloatti.Grid` (main), `Calloatti.Config` (simpleconfig)
- **No XML doc comments** on production code (unless explicitly requested)
- **No emojis** in code or docs (unless requested)
- **No README/doc files** created proactively
- **No commits** unless explicitly requested
- **Single-letter local variable names** acceptable in tight loops
- **Local functions** used for inline helpers in rendering code
