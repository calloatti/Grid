# AGENTS.md for Grid Mod

## Refactoring Plan

### 1. Directory Restructuring
- Create subfolders: `Services/`, `Tools/`, `Configurations/`, `UI/`
- Move tool files into `Tools/` subfolders (e.g., `Tools/Water/`)
- Place service files in `Services/`
- Move configuration files to `Configurations/`
- UI-related files in `UI/`

### 2. Namespace Consistency
- Maintain `Calloatti.Grid` as the main namespace
- Add sub-namespaces for features: `Calloatti.Grid.Water`, etc.

### 3. Feature Isolation
- Dedicated folders for each feature (Water, Ruler, Marker)
- Example: `Tools/Water/WaterPlannerTool.cs`

### 4. Shared Utilities
- Extract common code into `Utils/`

### 5. Configuration Management
- Centralize config files in `Configurations/`

### 6. Code Refactoring
- Eliminate duplication
- Create base classes for tools/services