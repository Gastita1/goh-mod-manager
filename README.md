GoH Mod Tools

Two small, portable tools for **Call to Arms – Gates of Hell** (AppID 400750).
No installer, no runtime, no background process — Windows 10/11 with the built-in .NET Framework 4.x.

[![Download](https://img.shields.io/badge/download-latest%20release-226e3a?style=for-the-badge)](../../releases/latest)
[![Steam Guide](https://img.shields.io/badge/steam-guide-1b2838?style=for-the-badge)](https://steamcommunity.com/sharedfiles/filedetails/?id=3836109112)

1. GoH Rule Set Maker — `GoHRuleMaker.exe`

Generate a **real game mod** that bans units from the buy menus.

| | |
|---|---|
| **1844 unit entries** | names in Chinese and English, taken straight from the game files |
| **unit icons** | decoded from the game's own TGA atlases |
| **filters** | faction · period · category, plus search and quick presets |
| **scopes** | multiplayer menu · AI purchase tables · conquest table · campaign tech tree |
| **AI ≠ players** | the two can be banned independently |
| **share codes** | `GOHR1:…` — teammates import it and produce the identical rule set |
| **self check** | after generating, warns if any unit reference is left dangling |
| **cleanup** | lists and deletes only the mods this tool created |
| **UI** | 中文 / English |

Quick presets: *no heavy tanks · no air support · no artillery · no vehicles · all non-infantry*.

Generated mods only touch purchase tables. Entity definitions, mission files and the
campaign tech tree (unless you tick that option) are never modified — campaign missions
and their scripted units stay exactly as they are.

2. GoH Mod Manager — `GoHModManager.exe`

- real load order with drag & drop (the game reads `options.set`, last entry wins)
- dependency check (`require`), version check (`minGameVersion` / `maxGameVersion`)
- file-conflict list for `resource/` and `localizations/`
- presets, and **share codes** so a co-op group syncs in seconds (`Ctrl+E` / `Ctrl+I`)
- automatic backup of `options.set` before every write, one-click restore
- mod details: Workshop author, subscribers, last update, description
- 8 UI languages
Install

1. Download the latest release zip
2. Extract anywhere (a folder on any drive)
3. Run `GoHRuleMaker.exe` or `GoHModManager.exe`

Both locate your Steam library automatically. If they don't, pick the game folder once
(`…\steamapps\common\Call to Arms - Gates of Hell`) — it is remembered.

Build from source

No SDK required — the tools are plain C# 5 and build with the compiler that ships with Windows:

```bat
src\rule-maker\build.cmd
src\manager\build.cmd

Notes

- Unofficial tools, not affiliated with Digitalmindsoft / Barbed Wire Studios.
- The manager writes only the game's `options.set` (backed up first) and its own config folder.
- The rule maker writes only into `<game>\mods\<name>\`.
- Valve removes executable content from the Workshop, so these tools live here on GitHub —
  the Workshop item is only an entry point (see the Steam Guide).

License

MIT — see [LICENSE](LICENSE).
