# MimesisMap

A dungeon map and minimap for **MIMESIS**. The building is put together from prefab rooms every run, and remembering the way back is hard: MimesisMap draws what you have explored, room by room.

**Only you need it.** It is fully client-side: the host and other players don't need the mod.

## Features

- **Map of the building, room by room.** A room appears as soon as you step into it, not only the area around you. Or switch to the **full map** from the start.
- **Minimap**: range, size, position, round or square, fixed north or rotating with your view.
- **Big map** (N): opens centred on you, mouse wheel to zoom, drag to pan, R to recentre, north-up or rotating. It stays clear of the game's HUD.
- **Exits**: building exits and entrance doors are marked, with the **shortest path to the nearest known exit** along the game's own navigation.
- **Stairs and floors**: staircases get an icon, other floors are dimmed.
- **Two styles**: clean floor plan (Plain) or a real top-down view with fog over unexplored rooms (Graphic).
- **Markers**: loot coloured by value, teammates and monsters (both off by default: teammate markers would give away mimics).
- **Shared exploration**: rooms where teammates have been open up too, and players who have the mod share their explored rooms through the Steam lobby. No host mod needed.
- Works in levels, the tram and the lobby.
- **All settings in game**: press **F8**, or use the "Карта (мод)" button in the game's settings window. Settings are stored in `UserData/MelonPreferences.cfg`.

## Controls (default, all rebindable)

| Key | Action |
|---|---|
| N | Big map (press or hold, configurable) |
| F6 | Minimap on/off |
| F8 | Settings window |
| Mouse wheel / + / - | Zoom the big map |
| Drag | Pan the big map |
| R | Recentre the big map on you |

## Install

- **r2modman / Gale**: install *MimesisMap* from Thunderstore.
- **Manually**: install [MelonLoader](https://github.com/LavaGang/MelonLoader/releases) 0.7.x, then put `MimesisMap.dll` into the game's `Mods` folder.

## Build from source

Requires the .NET SDK and the game installed. The project references the game's `MIMESIS_Data/Managed` assemblies and MelonLoader relative to its folder, so clone it into the game folder (`MIMESIS/MimesisMap`) or adjust `GameDir` in `MimesisMap.csproj`:

```
dotnet build -c Release
```

The build also copies the mod into the r2modman `Default` profile if it exists. `pack.ps1` creates the Thunderstore package in `dist/`.

## License and credits

Public domain ([The Unlicense](LICENSE)). If you build on it, a mention is appreciated but not required: idea by **StimDesu**, code written with the AI assistant **Claude** (Anthropic). See [ATTRIBUTION.md](ATTRIBUTION.md).

Thanks to [medovanx](https://github.com/medovanx/mimesis-patches) and [ToxesFoxes](https://github.com/ToxesFoxes/Mimesis-Minimap) for their minimap mods, which inspired several features.

---

## По-русски

Карта здания и миникарта для **MIMESIS**: комнаты открываются целиком, как только вы в них заходите, или вся карта сразу. На карте отмечены выходы с кратчайшим путём до ближайшего, лестницы, лут, по желанию союзники и монстры. Мод нужен только вам, хосту ставить его не обязательно. Все настройки открываются в игре по **F8**. Мод передан в общественное достояние; упоминание идеи (StimDesu) и того, что код написан с помощью ИИ Claude, — просьба, а не условие.
