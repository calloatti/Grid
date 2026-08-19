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
| `Source/EvapModule.cs` | Evaporation overlay configurator & input service |
| `Source/EvapService.cs` | Evaporation rate overlay — chunked rendering, hash dirty tracking, AssetBundle shader |
| `Source/MarkerModule.cs` | Marker configurator, input service, settings (binds `MarkerTool`, `MarkerToolClear`, `MarkerToolDeleteAll`) |
| `Source/MarkerService.cs` | Colored cross markers on columns, save/load persistence, `DeleteMarker(Vector3Int)` |
| `Source/RulerModule.cs` | Ruler configurator & input service (binds `RulerTool`, `RulerCircleTool`, `RulerToolClear`, `RulerToolDeleteAll`) |
| `Source/RulerService.cs` | Ruler segments, overlap management, sprite-based display, `ForceCircle`, `DeleteRulerAt(Vector3Int)` |
| `Source/BottomBarConfigurator.cs` | Registers `BottomBarButtonGroup` as `BottomBarModule` provider |
| `Source/BottomBarButtonGroup.cs` | Tool group UI — Marker, Ruler, Water tools via `ToolButtonFactory` |
| `Source/WaterTool*.cs` | Water tools (Planner, Eraser, Rise, Lower, DeleteAll) |
| `Source/RulerTool*.cs` | Ruler tools (Draw, Circle, Clear, DeleteAll) |
| `Source/MarkerTool*.cs` | Marker tools (Place/cycle color, Clear, DeleteAll) |
| `Source/RulerCircleTool.cs` | Circle ruler tool — sets `RulerService.ForceCircle = true` on Enter, mirrors `RulerTool` input |
| `Source/RulerToolClear.cs` | Clear Rulers tool — click a ruler to delete it via `RulerService.DeleteRulerAt()` |
| `Source/MarkerToolClear.cs` | Clear Markers tool — click a marker to delete it via `MarkerService.DeleteMarker()` |
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

## Evaporation (Game Data Model)

### Data Sources (all public, bound in Game + MapEditor contexts)
- `IThreadSafeWaterMap` (Timberborn.WaterSystem.cs) — water columns: `ColumnCounts[index2D]`, `ColumnCount(int index2D)`, `ColumnFloor(int index3D)`, `ColumnCeiling(int index3D)`, `WaterDepth(int index3D)`, `WaterColumns` (`ReadOnlyArray<ReadOnlyWaterColumn>`, each with `Floor`/`Ceiling`/`WaterDepth`)
- `IThreadSafeWaterEvaporationMap` (Timberborn.WaterSystem.cs) — `EvaporationModifiers` (`ReadOnlyArray<float>`) per water column
- Both are read from `index3D = index2D + j * VerticalStride` where `index2D = MapIndexService.CoordinatesToIndex((x, y))` and `j` = water-column index
- No publicization needed; `Timberborn.WaterSystem` and the two interfaces are public
- `WaterSimulatorSpec`, `SoilMoistureSimulatorSpec`, `ITickService`, `DayNightCycleSpec` are internal — do NOT rely on them; hardcode sim constants

### Water Column Model (critical)
- A **water column** is a continuous stack of water cells spanning `[Floor, Ceiling)` — one `WaterColumn` entry per stack, NOT one per z-level
- `WaterDepth` is the fill depth within that span (max = `Ceiling - Floor`); the whole stack is one column
- `ColumnCounts[index2D]` counts **columns per tile** (default 1, even dry tiles — an empty column with `WaterDepth == 0`). Obstacles split/merge columns (`SplitColumn`/`MergeColumns`/`InsertColumn`), so one tile can have multiple columns (e.g. ground water + a gapped elevated aqueduct)
- **Only the top cell of each column evaporates** (evaporation is applied once per column, reducing the column's `WaterDepth`). Water cells under other water cells do NOT evaporate
- Column top block surface = `Ceiling` (top cell z is `Ceiling - 1`)
- The evaporation modifier array is indexed per column: `EvaporationModifiers[index2D + j * VerticalStride]`

### Evaporation Computation
- Per column per sim step: `evaporation = evapSpeed × modifier × deltaTime`, subtracted from `WaterDepth` (`WaterParametersUpdateTask.ProcessWaterDepthChanges`)
- `evapSpeed` = `0.001` (fast) if `WaterDepth < 0.02` else `0.0001` (normal) — **per mod decision, always use the normal `0.0001` rate** (ignore the depth threshold)
- `modifier` comes from soil-moisture cluster saturation at that column
- **Modifier vs saturation (vanilla `SoilMoistureSimulator` spec, `QuadraticEvaporationCoefficient=0.0595`, `LinearQuadraticCoefficient=0.101`, `ConstantQuadraticCoefficient=0.72`):**

| saturation | watered neighbors | evap modifier |
|---|---|---|
| 0 (dry) | none | 1.0 (unreachable — no water column to evaporate) |
| 1 | ~1 | 6.45 |
| 2 | ~2 | 5.34 |
| 3 | ~3 | 4.34 |
| 4 | ~4 | 3.47 |
| 5 | ~5 | 2.71 |
| 6 | ~6 | 2.08 |
| 7 | ~7 | 1.56 |
| 8 (fully watered) | 8 | 1.16 |

- Direction is counterintuitive: modifier **decreases as moisture increases** — peak (6.45×) at saturation 1 (fresh/thin irrigation), near baseline (1.16×) when fully saturated. Dry soil has no water → no evaporation
- `wateredNeighbours[cell] = neighbor count + 1` for any water cell, so water columns always have saturation ≥ 1 → **reachable modifier range for evaporating columns is 1.16–6.45**

### Value Range (per column, m³/day)
- Per-day factor = `TickIntervalInSeconds(0.6) × DayLengthInTicks(768) = 460.8` (hardcoded)
- `m³/day = 0.0001 × modifier × 460.8`
- Realistic per-column range: **0.05–0.30 m³/day** (0.05 at saturated center, 0.30 at thin edge) → displayed 2 decimals (0.05–0.30)
- One cell (1×1 m) at 1 unit depth = 1 m³, so depth rate = m³/day per cell directly

### Evaporation Map (implemented — `Source/EvapModule.cs` + `Source/EvapService.cs`, mirrors TopoService)
- Per tile, iterate columns `j in 0..ColumnCounts[index2D]-1`; skip if `WaterDepth == 0`
- Display one number per water column at its top block surface: world y = `Ceiling + 0.05f`
- Multiple columns in one tile → one number each, at their own tops (stacked for continuous towers, separate for gapped stacks)
- Display value = `0.0001 × modifier × 460.8` m³/day, **2 decimals**, mapped to atlas row 3 (`EvapDataRow = 3`): `spriteIndex = Clamp(Round(evap × 100), 0, 255)` — atlas cells pre-baked `0.00`…`2.55` in magenta-purple `235,90,255`. NOT the topo/CG digit cells (those stay white).
- `index2D = MapIndexService.CoordinatesToIndex3D(new Vector3Int(x, y, 0))`; `index3D = index2D + j * MapIndexService.VerticalStride`
- **Chunked rendering (32×32 chunks):** subscribes `ITickableSingletonService.ForcedParallelTickFinished` (public, bound in Game+MapEditor) and refreshes ONE chunk per game tick; whole map cycles every `ceil(Size.x/32)×ceil(Size.y/32)` ticks. **Chunk-level hash dirty tracking** (rolling hash of water depth + modifier per column) — only rebuilds changed chunks.
- **Camera rotation:** handled by chunk-level mesh rotation (in-place vertex rotation with delta quaternion) — no full rebuild on camera rotate.
- **AssetBundle** with custom URP shader (`Shaders/EvapAtlas`) and material (`Materials/EvapAtlasMaterial`) deployed via `Resources/` folder; loads via `_assetLoader.Load<Material>("Materials/EvapAtlasMaterial")`.
- Hotkey: Shift+V (`Calloatti.Grid.KeyBind.Toggle.Evap`, order 35); notifications `Calloatti.Grid.EvapData.NotificationOn/Off`
- Slice visibility like topo (number visible when slice is at/below the column top); camera-rotation snap like topo
- **Version compatibility:** all APIs used (public `IThreadSafeWaterMap` subset, `IThreadSafeWaterEvaporationMap`, `ITickableSingletonService`, `ITickService`, `MapIndexService.VerticalStride`/`CoordinatesToIndex3D`) are identical in 1.0.13.1 and 1.1.2.1 — single `Version-1.0` implementation serves both, no `Version-1.1` folder needed. Water evaporates from the top cell of each column only.

## Key Game API Namespaces
- `Timberborn.BlockSystem` — `BlockObject`, `IBlockService`, `BlockOccupations`, events
- `Timberborn.Buildings` — `Building` component
- `Timberborn.ConstructionGuidelines` — `ConstructionGuidelinesRenderingService`, `CrossParameters`, `ConstructionGuidelinesSpec`
- `Timberborn.Coordinates` — `CoordinateSystem` (GridToWorld/WorldToGrid)
- `Timberborn.InputSystem` — `CursorService` (custom-cursor specs), `InputService`
- `Timberborn.SelectionToolSystem` — `SelectionToolProcessor`/`SelectionToolProcessorFactory` (drag tools, cursor by blueprint ID)
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
- **NEVER deploy anything** and **NEVER check the deployed mods folder** (e.g. `Documents\Timberborn\Mods`). The project build handles deployment/copy automatically.

## Conventions
- **Namespaces**: `Calloatti.Grid` (main), `Calloatti.Config` (simpleconfig)
- **No XML doc comments** on production code (unless explicitly requested)
- **No emojis** in code or docs (unless requested)
- **No README/doc files** created proactively
- **No commits** unless explicitly requested
- **Single-letter local variable names** acceptable in tight loops
- **Local functions** used for inline helpers in rendering code

## Cursor Handling (Two Mechanisms)

**Code method (click-based tools — no blueprint needed):** `MarkerTool`, `RulerTool`, `WaterToolRise/Lower`, `MarkerToolClear`, `RulerToolClear`, `RulerCircleTool` load the texture directly via `IAssetLoader.Load<Texture2D>("Resources/ui/cursors/<name>")` and set it with Unity's `Cursor.SetCursor(_cursor, hotspot, CursorMode.Auto)` in `Enter()` / `null` in `Exit()`. The path is just an asset path; blueprint JSON not involved.

**Blueprint method (drag-based water tools — blueprint required):** `WaterToolPlanner`/`WaterToolEraser` use `SelectionToolProcessorFactory.Create(..., cursorId)`. `SelectionToolProcessor.Enter()` calls `CursorService.SetCursor(id)`, which does `_cursorSpecs[id]` dictionary lookup built from `CustomCursorSpec` blueprints (Timberborn.InputSystem.cs) — missing ID throws `KeyNotFoundException`. The eraser uses the vanilla `"CancelCursor"` spec (already in game; no custom blueprint needed).

**Vanilla cursor reuse (no added assets):** All clear tools use the game's built-in cancel cursor. Click-based tools (`MarkerToolClear`, `RulerToolClear`) load the vanilla texture directly via `IAssetLoader.Load<Texture2D>("UI/Cursors/CancelCursorLarge")` (resolved by `ResourceAssetProvider` → `Resources.Load`). The eraser passes the vanilla spec ID `"CancelCursor"`. Do NOT add custom cursor blueprints/PNGs for cursors that already exist in the game.

## Shared Delete Logic (No Duplication)

Single-marker/single-ruler deletion is implemented once in each service and shared by both the in-tool hotkey branch and the Clear tool:
- `MarkerService.DeleteMarker(Vector3Int)` — used by `MarkerTool` shift-click and `MarkerToolClear`
- `RulerService.DeleteRulerAt(Vector3Int)` — used by `RulerTool` shift-click (`HandleClick`) and `RulerToolClear`

## Circle Ruler Feature

### Implementation
- **ALT+click** while placing a ruler creates a circle ruler with center at the start tile and radius = current tile distance
- **Circle ruler tool** (`Source/RulerCircleTool.cs`) sets `RulerService.ForceCircle = true` on `Enter()` / `false` on `Exit()`, then mirrors `RulerTool` click/move input so no ALT key is needed
- `RulerService.ForceCircle` is OR'd with `IsAltPressed()` in `HandleClick`/`HandleMouseMove` (no ALT = normal ruler, ALT or ForceCircle = circle)
- Circle preview uses atlas index 0 (empty square)
- Diameter line shows numbered ticks (1..diameter) including axis endpoints
- Circle outline skips the 4 cardinal axis endpoints (diameter line handles those)
- File: `Source/RulerService.cs`

### Circle Algorithm (matches donatstudios.com reference)
The algorithm that matches the donatstudios.com Pixel Circle Generator SVG output:
1. Build a filled circle using the **pixel-center method** with radius `r+0.5`: a tile at offset `(dx,dy)` from center is inside the filled region if `dx²+dy² ≤ (r+0.5)²`
2. The circle outline is the **8-connected border** of this filled region: tiles that have at least one 8-neighbor outside the filled region
3. Skip the 4 cardinal axis endpoints from the circle tiles (the diameter line handles those with numbers)

This produces exactly `8*r` tiles for radius `r`, matching the SVG reference files in `.scratch/`.

### Key Code
```csharp
// GetCircleTiles in RulerService.cs
float outerRSq = (radius + 0.5f) * (radius + 0.5f);
// Fill: dx*dx + dy*dy <= outerRSq
// Border: at least one 8-neighbor outside the filled set
// Skip cardinal endpoints: (dx==0 && |dy|==radius) || (dy==0 && |dx|==radius)
```

### SVG Validation
Reference SVG files from donatstudios.com are in `.scratch/` (e.g., `Circle-7x7-download.svg`).
SVG parsing: use independent regex for each attribute (`data-x`, `data-y`, `fill`) rather than assuming attribute order.
Convert absolute SVG coordinates to relative by subtracting the center offset `(radius, radius)`.

## Hard Rule
DO NOT EVER TOUCH THE DEPLOY FOLDER.

BUILD DOES EVERYTHING, NEVER EVER MESS WITH THE DEPLOY PROCESS.
