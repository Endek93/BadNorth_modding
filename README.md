# Bad North Mods

《坏北：Jotunn Edition》Windows 版 Mod。每个 Mod 都可单独下载和安装。

## 下载 / Downloads

| Mod | 下载 |
| --- | --- |
| [Campaign Gold](#campaign-gold) | [下载 ZIP](https://raw.githubusercontent.com/Endek93/BadNorth_modding/main/downloads/Campaign_Gold-1.0.0.zip) |
| [Island Travel](#island-travel) | [下载 ZIP](https://raw.githubusercontent.com/Endek93/BadNorth_modding/main/downloads/Island_Travel-1.1.0.zip) |
| [Mod Menu](#mod-menu) | [下载 ZIP](https://raw.githubusercontent.com/Endek93/BadNorth_modding/main/downloads/Mod_Menu-1.1.1.zip) |
| [Musketeers](#musketeers) | [下载 ZIP](https://raw.githubusercontent.com/Endek93/BadNorth_modding/main/downloads/Musketeers-0.7.9.zip) |
| [Save Compatibility](#save-compatibility) | [下载 ZIP](https://raw.githubusercontent.com/Endek93/BadNorth_modding/main/downloads/Save_Compatibility-1.0.0.zip) |
| [Unlocks](#unlocks) | [下载 ZIP](https://raw.githubusercontent.com/Endek93/BadNorth_modding/main/downloads/Unlocks-1.0.0.zip) |

## 手动安装 / Manual installation

1. 给 Windows 版《坏北》安装 [BepInEx 5（Windows x86）](https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.6/BepInEx_win_x86_5.4.23.6.zip)。
2. 下载你选中的 Mod ZIP，关闭游戏后解压，把里面的 `plugins` 文件夹合并到游戏的 `BepInEx` 目录。如果 ZIP 里有 `patchers`，也一起合并到 `BepInEx`。
3. 启动游戏。每个 ZIP 都可独立安装，Mod Menu 的启动补丁已随包附带。

Install BepInEx 5 (x86), unzip the mod, then merge its `plugins` folder and any `patchers` folder into the game's `BepInEx` folder. Restart the game.

## 功能介绍

### Campaign Gold

直接在战役地图上调整当前战役的金币余额。

- **F6** 默认每次增加 **100 金币**。**F7** 默认把余额设为 **999 金币**。
- 在 `BepInEx/config/local.badnorth.campaign-gold.cfg` 里可以改快捷键、每次增减的金额和目标余额。把增量设为负数即可扣除金币。
- 余额变化会立即显示在升级界面，并保存到当前战役。

### Island Travel

打开战役地图上的岛屿，让你自由选择挑战顺序。

- 可以前往原本还未解锁、尚未显示或位于维京推进线后方的岛屿。跳着打岛时，战斗进度照常保存。
- 默认在进入下一轮时固定维京推进线。想保留推进节奏，可以在 `BepInEx/config/local.badnorth.island-travel.cfg` 里把 `FreezeFrontier` 改为 `false`。岛屿通行功能仍然可用。

### Mod Menu

在游戏主菜单查看和管理已安装的本地 Mod。

- 点击 **模组**，或在主菜单按 **F10** 打开。列表会显示插件是否已加载，以及是否检测到文件冲突。
- 选择要开启或关闭的插件，点 **保存** 后重启游戏，新的选择就会生效。

### Musketeers

给弓箭手添加一条火枪手升级分支。弓箭手升到倒数一阶后，可以在战役升级界面选火枪手作为最终升级。

- 火枪采用直射弹道。射手会对移动敌人计算提前量，并在开火前检查射线遮挡。
- 单发伤害和击退更强，开火后每名火枪手有 **5 秒**装填时间。打到盾牌时伤害会衰减。
- 配有蓝色军服、火枪模型、装填动作、枪口烟雾和独立枪声。

### Save Compatibility

用于处理卸载 Mod 后，玩家档案里残留的失效升级。

- 加载档案时会清理当前安装缺失的收藏升级、初始特质和初始物品条目。
- 如果清理了条目，插件会写回玩家档案。**使用前请先备份存档。**

### Unlocks

开放收藏和新战役的初始配置，方便直接试用不同搭配。

- 收藏界面会显示已安装升级的最高等级。
- 在新战役的指挥官选择界面，可以从当前安装提供的特质、物品和消耗品中选择开局搭配。

## 音效署名

火枪手音效改编自 J8054 的《musket_fire_9.wav》，采用 CC BY 4.0。安装包附有完整署名及改动说明。 [原始音效](https://freesound.org/people/J8054/sounds/648755/) · [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) · [完整署名](BepInEx/plugins/Musketeers/Musketeers-Audio-Credits.txt)
