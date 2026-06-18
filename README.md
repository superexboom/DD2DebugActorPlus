# DD2 Debug Actor Plus

[中文说明](README.zh-CN.md)

DD2 Debug Actor Plus is a standalone BepInEx 5 actor inspection and editing tool for *Darkest Dungeon II*. It replaces and expands the built-in actor debug editor with a more readable Chinese UI, localization-aware labels, searchable lists, and direct editing tools.

This project is for local debugging and mod development. It is not affiliated with Red Hook Studios.

## Features

- Opens in and out of combat.
- Inspect and edit actors, equipped skills, combat items, trinkets, quirks, tokens, buffs, DOTs, stress, HP, and related state where runtime APIs allow it.
- Localized names and descriptions where available, with internal ids shown beside names.
- Current-state views for actor tokens, buffs, DOTs, and quirks so removal is easier.
- Pinyin and acronym search support for Chinese names.
- Larger, resizable Chinese UI compared with the built-in debug actor panel.

## Hotkey

| Key | Action |
| --- | --- |
| `Alt+J` | Open or close Debug Actor Plus |

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
- If another mod patches the same debug actor UI, patch order can matter.
