# DD2 Debug Actor Plus

[中文说明](README.zh-CN.md)

DD2 Debug Actor Plus is a standalone BepInEx 5 actor inspection and editing tool for *Darkest Dungeon II*. It replaces the built-in actor debug editor (Alt+J) with a game-styled editor window: game fonts, icons and hero portraits, searchable libraries with previews, and a hero relationship editor.

This project is for local debugging and mod development. It is not affiliated with Red Hook Studios.

## Features

- Opens in and out of combat. The window is a modal uGUI canvas drawn with the game's own TextMeshPro fonts, so clicks, hotkeys and Esc no longer leak into the game while it is open.
- Actor column on the left: heroes and enemies stacked (mouse-wheel scroll), each with portrait, health and stress; the tab bar shows which actor is being edited.
- **Health / Stress**: deal damage, heal, add or relieve stress, set an exact value, quick full / clear buttons.
- **Tokens, Buffs, DOTs, Quirks, Items**: the same three-column layout on every tab:
  - left: what the selected actor currently has, each with a Remove button (click one to find it in the library);
  - middle: the full library with icons, category filters, and name / id / pinyin / initials search (virtualised, no row limit);
  - right: preview with the game's own description, including its inline token icons (taken from the game's own icon atlas), and the Add / Remove buttons.
  - Double-click a library row to add it, right-click to remove it from the actor.
- **Relationships** (restored from the official editor and redesigned):
  - relationships are shown per hero pair (`A ⇄ B`): the game stores one affinity value and one relationship per pair, shared by both heroes;
  - the selected hero is always on the left of each card; filter between "pairs with this hero" and "all pairs";
  - affinity bar split into the game's leaning levels, ±1 / ±5 / min / start / max buttons, and the base chance the current level rolls when the party sets out from an inn;
  - **one-click clear** of the current relationship;
  - **one-click assign**: a single scale from the worst relationship to the best (Hateful … Suspicious · Neutral · Hopeful … Amorous). Clicking applies the relationship to both heroes through the game's normal path (skill modifiers, act-outs, buffs) and, optionally, moves the affinity value to the matching level.
- Bilingual UI (English / Chinese). `auto` follows the game language; the header button switches it.

## Hotkeys

| Key | Action |
| --- | --- |
| `Alt+J` | Open or close Debug Actor Plus (configurable) |
| `Esc` | Close the editor (or leave the search field) |

## Configuration

`BepInEx/config/com.dd2.debugactorplus.cfg`

| Setting | Default | Meaning |
| --- | --- | --- |
| `General.Language` | `auto` | `auto` (follow the game), `zh`, `en` |
| `General.ToggleKey` | `J + LeftAlt` | Hotkey that opens / closes the editor |
| `Appearance.UiScale` | `1` | Window scale relative to a 1600×900 layout (0.6–1.6); the default stays readable at 1280×720 |
| `Appearance.Frames` | empty | Optional frame overrides, e.g. `Panel=ui_tooltip@5; Button=ui_button_black@5`. Roles: Window, Panel, Card, CardSelected, Button, ButtonPrimary, ButtonDanger, Chip, ChipSelected, Field, ScrollHandle, Divider; value = game sprite name `@` border shrink factor, or `none`. Empty = the built-in native frames. **Reload** in the editor applies changes without restarting. |
| `Diagnostics.SpriteGallery` | `false` | Adds a "Sprites" tab showing every loaded 9-slice game sprite as a frame with its name, for choosing `Frames` |
| `Diagnostics.DumpUiSprites` | `false` | Also log those sprite names when the editor first opens |

## Installation

Extract the release zip into the game directory.

Expected release layout:

```text
Darkest Dungeon II/
└─ BepInEx/
   └─ plugins/
      └─ DD2DebugActorPlus/
         └─ DD2DebugActorPlus.dll
```

## Requirements

- *Darkest Dungeon II*
- BepInEx 5.x, tested with BepInEx 5.4.23.5
- Unity/Mono BepInEx build, not IL2CPP
- .NET SDK or build tools capable of building `net48`
- Local game assemblies from `Darkest Dungeon II_Data/Managed`

## Build

1. Copy `Directory.Build.props.example` to `Directory.Build.props`.
2. Set `BepInExDir` and `ManagedDir`.
3. Build:

```powershell
dotnet build .\DebugActorPlus\DebugActorPlus.csproj -c Release
```

Game assemblies, decompiled game source, exported assets, local paths, and build outputs are intentionally excluded.

## Notes

- This is a powerful debug tool and can easily create invalid run state.
- Some definitions only load after the game has entered an appropriate scene or run state.
- Relationships only exist between heroes of the current party during a run. Outside the Kingdom mode the game resets relationships when the party enters an inn and rolls new ones when it sets out again.
- Applying a relationship makes the game save the current run, as it does when a relationship forms normally.
- Frames, buttons, scrollbars and dividers use the game's own UI sprites (those the game keeps loaded from the main menu on); fonts, icons and portraits come from the game too. If a sprite is missing (e.g. after a game update) that element falls back to a drawn frame.
- If another mod patches the same debug actor UI, patch order can matter.
