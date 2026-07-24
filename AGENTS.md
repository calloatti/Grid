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

## Key Game API Namespaces
- `Timberborn.BlockSystem` — `BlockObject`, `IBlockService`, `BlockOccupations`, events
- `Timberborn.Buildings` — `Building` component
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
