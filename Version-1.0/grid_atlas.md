# Grid Atlas Layout

**File:** `Resources/Sprites/grid-atlas.png`  
**Dimensions:** 256 columns × 6 rows (each cell = 1 sprite)  
**Total sprites:** 1,536 (256 × 6)

## Atlas Coordinate System

UV mapping (Unity convention, origin bottom-left):
- `vTop = 1.0f - (row / 6)`
- `vBottom = 1.0f - ((row + 1) / 6)`
- Row 0 = top row, Row 5 = bottom row

| Row Index | vTop    | vBottom | Purpose | Used By |
|-----------|---------|---------|---------|---------|
| 0         | 1.000   | 0.833   | **Ruler numbers & circle preview** (0–255) | `RulerService` (`CircleAtlasIndex = 0`, `RULER_LENGTH = 255`) |
| 1         | 0.833   | 0.667   | (unused / reserved) | — |
| 2         | 0.667   | 0.500   | **Topography numbers** (0.00–2.55) | `TopoService` (`TopoDataRow = 2`) |
| 3         | 0.500   | 0.333   | **Evaporation rates** (0.00–2.55 m³/day) | `EvapService` (`EvapDataRow = 3`) |
| 4         | 0.333   | 0.167   | (unused / reserved) | — |
| 5         | 0.167   | 0.000   | **Construction guideline numbers** (1–30+) | `CGService` (`NUMBER_ROW = 5`) |

## Service Details

### TopoService (`Source/TopoService.cs:22`)
- Constant: `TopoDataRow = 2`
- Uses columns 0–255 for values 0.00–2.55 (height in meters)
- Sprite index = `Clamp(Round(height × 100), 0, 255)`
- Shader: standard URP lit (white digits)

### EvapService (`Source/EvapService.cs:245`)
- Constant: `EvapDataRow = 3`
- Uses columns 0–255 for values 0.00–2.55 (m³/day per column)
- Sprite index = `Clamp(Round(evap × 100), 0, 255)`
- Shader: custom `EvapAtlas` (magenta-purple 235,90,255 digits)

### CGService (`Source/CGService.cs:19`)
- Constant: `NUMBER_ROW = 5` (bottom row)
- Uses columns 1–30+ for distance numbers (1, 2, 3… up to radius 30)
- Shader: standard URP lit (white digits)

### RulerService (`Source/RulerService.cs:41-42`, `830-833`)
- Uses **row 0 only** (all values fit in 0–255)
- `CircleAtlasIndex = 0` → row 0, col 0 (circle preview)
- `RULER_LENGTH = 255` → numbered ticks 1–255 use row 0, col 1–255
- `AdjustSegmentUVs()` computes `row = logicalValue / 256` (always 0), `col = logicalValue % 256`
- Dynamic row support exists but unused since max value = 255
- Shader: standard URP lit

## Column Mapping

All services share the same 256-column layout:
- Columns 0–255 → sprite indices 0–255
- Value-to-sprite: `spriteIndex = Clamp(Round(value × 100), 0, 255)` for Topo/Evap
- CG uses direct column = number (1, 2, 3…)
- Ruler encodes row+col into single `logicalValue`

## Color Schemes

| Row | Service | Color | Shader |
|-----|---------|-------|--------|
| 0   | Ruler   | White | Standard URP |
| 2   | Topo    | White | Standard URP |
| 3   | Evap    | Magenta-purple (235,90,255) | Custom `EvapAtlas` |
| 5   | CG      | White | Standard URP |

## Notes

- Rows 1, 4 are currently unused — available for future overlays
- Atlas is loaded via `IAssetLoader.Load<Texture2D>("Sprites/grid-atlas")`
- All services bind the same texture to their respective materials
- Changing atlas layout requires updating all four services' row constants
- RulerService dynamic row calculation is redundant (all values ≤ 255 → row 0)