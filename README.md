# Bad North Mods

《坏北：Jotunn Edition》Windows 版 Mod。Each mod is available as a separate ZIP.

## 下载 / Downloads

| Mod | 版本 | 下载 | 源码 | 功能 |
| --- | --- | --- | --- | --- |
| Campaign Gold | 1.0.0 | [下载 ZIP](https://raw.githubusercontent.com/Endek93/BadNorth_modding/main/downloads/Campaign_Gold-1.0.0.zip) | [源码](ModDevelopment/Mods/CampaignGold) | 战役地图上按 F6 增加金币，按 F7 设置余额。 |
| Island Travel | 1.1.0 | [下载 ZIP](https://raw.githubusercontent.com/Endek93/BadNorth_modding/main/downloads/Island_Travel-1.1.0.zip) | [源码](ModDevelopment/Mods/IslandTravel) | 解除岛屿出行限制，可配置维京推进线。 |
| Mod Menu | 1.1.1 | [下载 ZIP](https://raw.githubusercontent.com/Endek93/BadNorth_modding/main/downloads/Mod_Menu-1.1.1.zip) | [源码](ModDevelopment/Mods/ModMenu) | 在主菜单管理本地 Mod，重启后应用变更。ZIP 已包含启动补丁。 |
| Musketeers | 0.7.9 | [下载 ZIP](https://raw.githubusercontent.com/Endek93/BadNorth_modding/main/downloads/Musketeers-0.7.9.zip) | [源码](ModDevelopment/Mods/Musketeers) | 为弓箭手加入火枪手升级分支。 |
| Save Compatibility | 1.0.0 | [下载 ZIP](https://raw.githubusercontent.com/Endek93/BadNorth_modding/main/downloads/Save_Compatibility-1.0.0.zip) | [源码](ModDevelopment/Mods/SaveCompatibility) | 清理缺失的存档升级条目。使用前请备份存档。 |
| Unlocks | 1.0.0 | [下载 ZIP](https://raw.githubusercontent.com/Endek93/BadNorth_modding/main/downloads/Unlocks-1.0.0.zip) | [源码](ModDevelopment/Mods/Unlocks) | 开放收藏和开局装备选项。 |

## 手动安装 / Manual installation

1. 给 Windows 版《坏北》安装 [BepInEx 5.4.23.6（Windows x86）](https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.6/BepInEx_win_x86_5.4.23.6.zip)。
2. 下载你选中的 Mod ZIP，关闭游戏后解压，把里面的 `plugins` 文件夹合并到游戏的 `BepInEx` 目录。如果 ZIP 里有 `patchers`，也一起合并到 `BepInEx`。
3. 启动游戏。每个 ZIP 都可独立安装，Mod Menu 的启动补丁已随包附带。

Install BepInEx 5 (x86), unzip the mod, then merge its `plugins` folder and any `patchers` folder into the game's `BepInEx` folder. Restart the game.

## 源码与资源 / Source and assets

- [构建说明](ModDevelopment/) · [各 Mod 的源码](ModDevelopment/Mods/)
- [火枪手美术源文件](ModDevelopment/Mods/Musketeers/Art/) · [火枪手内嵌资源](ModDevelopment/Mods/Musketeers/Assets/)
- [单独取用 DLL](BepInEx/plugins/) · [Mod Menu 启动补丁](BepInEx/patchers/)

火枪手音效改编自 J8054 的《musket_fire_9.wav》，采用 CC BY 4.0。安装包附有完整署名及改动说明。 [原始音效](https://freesound.org/people/J8054/sounds/648755/) · [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) · [完整署名](BepInEx/plugins/Musketeers/Musketeers-Audio-Credits.txt)
