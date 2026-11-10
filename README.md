# Keys Not Included — Oxygen Not Included mod

> **Disclaimer:** This mod was developed by Claude, an AI model made by Anthropic.

Keys Not Included adds hotkeys and better UI defaults. It only changes controls and the UI;
power and building defaults (Smart Battery thresholds, switches starting off, power list
navigation) are in the separate **Power Not Included** mod.

## Hotkeys

All hotkeys are in the game's own **Options → Controls** screen, in the **Mods** section (each one
starts with "Keys Not Included:"). You can rebind or clear them there like any other game hotkey,
and the game saves them with its own bindings.

| Action                                        | Default   | Works when                                   |
|-----------------------------------------------|-----------|----------------------------------------------|
| Deconstruct selected (press again to cancel)  | Delete    | a building is selected                       |
| Cancel selected construction                  | Backspace | a planned / unfinished building is selected  |
| Deconstruct background buildings              | Shift+X   | always                                       |

- **Deconstruct selected** does exactly what the Deconstruct button in the building's info
  panel does: it marks the building for deconstruction, and pressing it again cancels the order.
- **Cancel selected construction** does what the Cancel button on a planned or unfinished
  building does: the order is removed and delivered materials are dropped.
- **Deconstruct background buildings** picks the Deconstruct tool with its filter on the
  background layer (drywall, tempshift plates, ...), leaving any overlay first if needed. This
  is temporary: the next time you pick Deconstruct normally (X or the toolbar), it goes back to
  the filter it had before.
- A key that doesn't apply right now (nothing suitable selected) does nothing and is passed on to
  the game.

## Options

Open them with the mod's **Options** button in the main-menu **Mods** screen. They're saved to
`config.json` in the mod folder and read each time a save loads.

### Default tool filters

| Option                              | Default      |
|-------------------------------------|--------------|
| Deconstruct                         | Buildings    |
| Cancel                              | Game default |
| Prioritize                          | Game default |
| Apply every time a tool is picked   | Off          |

The default is applied the first time you pick the tool after loading a save. If you then choose
another filter yourself, it sticks for the rest of the session (unless *Apply every time* is on).
In an overlay (Power, Plumbing, ...) the game picks the filter for that overlay; the mod leaves
that alone and applies the default the next time you pick the tool outside the overlay.

### Controls screen

**Esc clears a key binding** (default: On). In Options → Controls, click a binding so it says
"Press a key", then press Esc: the binding is set to None (unbound). Without this option, Esc only
cancels. It works for every hotkey, the game's and other mods'. Press Esc again (with no binding
waiting) to close the screen as usual.

### Build menu overlays

Opening one of these build menu categories, by its hotkey or by clicking it, switches to its
overlay. For example, **2** opens Oxygen and switches to the Oxygen (breathability) overlay. Each
one can be turned off separately; all are on by default.

| Build menu  | Overlay     |
|-------------|-------------|
| Oxygen      | Oxygen      |
| Power       | Power       |
| Plumbing    | Plumbing    |
| Ventilation | Ventilation |
| Automation  | Automation  |
| Shipping    | Shipping    |
| Radiation   | Radiation (Spaced Out) |

**Leave overlay when the build menu closes** (On) turns the overlay off again when you close the
build menu or open a category without an overlay. If you changed overlay yourself in the meantime,
or picked a building that the game shows in its own overlay, yours is kept.

## Adding more

- **A new hotkey:** add a line in `Hotkeys.Register()` (`src/Hotkeys.cs`). It appears in
  Options → Controls automatically.
- **A new option:** add a property to `KeysOptions` (`src/KeysOptions.cs`). It appears in the
  Options screen automatically.

## Build in Visual Studio (Windows)

Uses `netstandard2.1`, paths in `Directory.Build.props`, installs to `mods\Local`, PLib merged
in with ILRepack. Visual Studio restores the PLib and ILRepack NuGet packages on the first build;
if it doesn't, right-click the solution → **Restore NuGet Packages**.

1. Open **`Directory.Build.props`** and check `GameFolder`. It is pre-filled with
   `D:\SteamLibrary\steamapps\common\OxygenNotIncluded\OxygenNotIncluded_Data\Managed`.
   If Documents is in OneDrive, change `ModFolder` as the comment says.
2. Double-click **`KeysNotIncluded.sln`** in Visual Studio 2022.
3. **Build → Rebuild Solution**. Debug or Release both work.
4. The Output window ends with `KeysNotIncluded installed to ...\mods\Local\DEV_KeysNotIncluded`.
5. Start the game, open **Mods**, enable **Keys Not Included**, restart.

Build with the game closed (it locks the DLL while running).
Log: `%USERPROFILE%\AppData\LocalLow\Klei\Oxygen Not Included\Player.log` (lines start with `[KeysNotIncluded]`).

## If a game update breaks it

Klei changes game internals between versions (for example, the tool filter menu went from a
`Dictionary` of states to a `ToggleData` array). Anything not part of the game's public surface is
reached by reflection with fallbacks, and each feature logs one warning and switches itself off
instead of crashing if it can't find what it needs:

- Tool filters: `ToolParameterMenu` widgets and states (`src/FilterMenu.cs`); a filter is selected by
  invoking its widget's `onClick`, like a mouse click.
- Deconstruct selected: `Deconstructable.OnDeconstruct` (the info panel button), falling back to
  `IsMarkedForDeconstruction` / `QueueDeconstruction` / `CancelDeconstruction`.
- Esc clears a binding: Harmony prefixes on `InputBindingsScreen.OnKeyDown` / `Update` (only if the
  screen declares them), field `waitingForKeyPress`, method `Bind(KKeyCode, Modifier)`,
  `GameInputMapping.KeyBindings` / `SaveBindings()`, `Global.GetInputManager().RebindControls()`,
  `BuildDisplay()`. The screen's own `Bind` is run with F15 (never conflicts), then that entry is set
  to None; if the last step fails the action stays on F15, which behaves as unbound.
- Build menu overlays: `PlanScreen.ActiveCategoryToggleInfo` (fallback field `activeCategoryInfo`),
  `ToggleInfo.userData`, `OverlayModes.<Mode>.ID`.

Game APIs used directly (the compiler points at any that change):

- `PlayerController.Instance.ActiveTool` / `ActivateTool`, `InterfaceTool.ActivateTool` (Harmony postfix),
  `FilteredDragTool`, `DeconstructTool.Instance`
- `ToolMenu.Instance`, `ToolParameterMenu` (nested `FILTERLAYERS`, `ToggleState`), `MultiToggle.onClick`
- `SelectTool.Instance.selected`, `Deconstructable`, `Constructable`, `GameHashes.Cancel`
- `OverlayScreen.Instance.GetMode()` / `ToggleOverlay(HashedString, bool)`, `OverlayModes.None.ID`
- `PlanScreen.Instance`, `HashedString`
- `KScreen` (`OnKeyDown`, `Activate`), `KButtonEvent.TryConsume` / `IsAction`, `GameScreenManager.Instance.ssOverlayCanvas`
- Harmony patch on `Game.OnSpawn`

## Files

```
KeysNotIncluded.sln         open this in Visual Studio
Directory.Build.props       YOUR paths (GameFolder / ModFolder)
Directory.Build.targets     merges PLib.dll into the mod DLL
mod.yaml                    title / description / staticID
mod_info.yaml               version, API version, DLC support (all)
KeysNotIncluded.csproj
src/Mod.cs                  UserMod2 entry, PLib registration, Harmony patches
src/KeysOptions.cs          Options screen fields (config.json)
src/Hotkeys.cs              key bindings (PLib actions) + the screen that receives key presses
src/SelectedObject.cs       deconstruct / cancel construction of the selected object
src/BackwallDeconstruct.cs  Shift+X: Deconstruct on the background layer
src/DefaultFilters.cs       default filter per tool, applied after loading a save
src/FilterMenu.cs           reads / selects filters in the tool filter menu
src/FilterLayer.cs          the filter layers, as shown in Options
src/ControlsScreenEsc.cs    Esc in Options > Controls clears the binding
src/BuildMenuOverlay.cs     overlay per build menu category
```
