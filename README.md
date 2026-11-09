# GoH Mod Tools

Two small portable tools for **Call to Arms - Gates of Hell** (AppID 400750).
No installer, no runtime, no background process. Windows 10/11 + .NET Framework 4.x.

**Download:** https://github.com/Gastita1/goh-mod-manager/releases/latest

**Steam Guide:** https://steamcommunity.com/sharedfiles/filedetails/?id=3836109112

---

## 1. GoH Rule Set Maker — GoHRuleMaker.exe

Generates a real game mod that bans units from the buy menus.

- 1844 unit entries, names in Chinese and English taken from the game files
- unit icons decoded from the game's own TGA atlases
- filters: faction, period, category; search; quick presets
  (no heavy tanks / no air support / no artillery / no vehicles / all non-infantry)
- scopes can be combined: multiplayer menu, AI purchase tables, conquest table, campaign tech tree
- **AI and players can be banned independently**
- rule share codes (GOHR1:...) so a group can reproduce the same rule set
- post-generation self check: warns if a banned unit is still referenced somewhere
- cleanup dialog that lists and deletes only the mods this tool created
- Chinese / English UI

Generated mods only touch purchase tables. Entity definitions, mission files and the
campaign tech tree (unless that option is ticked) are never modified.

## 2. GoH Mod Manager — GoHModManager.exe

- real load order with drag and drop (the game reads options.set, last entry wins)
- dependency check, version check, file-conflict list
- presets and share codes so a co-op group syncs in seconds (Ctrl+E / Ctrl+I)
- automatic backup of options.set before every write, one-click restore
- mod details: Workshop author, subscribers, last update, description
- 8 UI languages

## Install

1. Download the latest release zip
2. Extract anywhere
3. Run GoHRuleMaker.exe or GoHModManager.exe

Both find your Steam library automatically. If not, pick the game folder once.

## Build from source

No SDK required, plain C# 5 compiled with the csc.exe that ships with Windows.
See src/manager/build.cmd and src/rule-maker/build.cmd.

## Notes

- Unofficial tools, not affiliated with Digitalmindsoft / Barbed Wire Studios.
- The manager writes only the game's options.set (backed up first) and its own config folder.
- The rule maker writes only into <game>\mods\<name>\.
- Valve removes executable content from the Workshop, so these tools are distributed here.

## License

MIT

---

# 中文

《地狱之门》两个绿色小工具，无需安装、无后台进程。

**下载：** https://github.com/Gastita1/goh-mod-manager/releases/latest

## 一、GoHRuleMaker.exe — 规则集生成器

勾选要禁用的单位，生成一个真正的游戏模组。

- 1844 条单位条目，中英文名称直接取自游戏文件，带单位图标
- 按阵营、时期、类别筛选，支持搜索与快捷按钮
- 生效范围可组合：联机购买菜单 / AI 采购表 / 征服购买表 / 战役科技树
- **AI 与玩家可以各自独立禁用**
- 规则分享码（GOHR1:...），队友导入即可生成完全相同的规则
- 生成后自检：有悬空引用会提示
- 清理功能只删除本工具生成的模组
- 中英双语界面

生成的模组只修改购买表；单位实体、任务文件、战役科技树（除非勾选）都不会被改动。

## 二、GoHModManager.exe — 模组管理器

加载顺序拖拽排序、依赖与版本检查、文件冲突列表、预设与联机分享码、
每次写入前自动备份可一键还原、模组详情、8 国语言界面。

## 安装

下载 Release 压缩包，解压到任意目录，双击运行即可。工具会自动找到游戏目录。

## 说明

非官方工具，与开发商无关。管理器只写游戏的 options.set（写前备份）和自己的配置；
生成器只写游戏目录下的 mods\<名字>\。
