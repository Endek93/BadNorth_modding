# BadNorth_modding

Personal mods for Bad North: Jotunn Edition.

## 安装

1. 给 Windows 版《坏北》安装 BepInEx 5（x86）。本地使用的版本是 5.4.23.6。
2. 在仓库首页点击 **Code → Download ZIP**，解压后，把需要的 Mod 文件夹从 `BepInEx/plugins` 复制到游戏的同名目录。
3. 如果安装 Mod Menu，还要把 `BepInEx/patchers/BadNorth.ModMenu.Bootstrap.dll` 复制到游戏的 `BepInEx/patchers`。复制完成后启动游戏。

| Mod | 功能 |
| --- | --- |
| Campaign Gold | 在战役地图上用 F6 加金币、F7 设置金币余额。 |
| Island Travel | 解除战役岛屿的出行限制，可配置是否冻结维京推进线。 |
| Mod Menu | 在游戏中管理本地插件。启停变更会在重启时应用。 |
| Musketeers | 添加火枪手升级分支和相应的美术、枪声及战斗效果。 |
| Save Compatibility | 处理存档中当前安装缺失的升级。插件可能重写玩家存档，使用前请备份。 |
| Unlocks | 开放收藏和战役开局装备选项。 |

## 从源码构建

源码在 `ModDevelopment/Mods`。安装 .NET SDK 后，在仓库根目录运行以下 PowerShell 命令。将 `$gameDirectory` 设成已经安装 BepInEx 的游戏目录，并按需替换项目路径。

```powershell
$gameDirectory = "D:\SteamLibrary\steamapps\common\BadNorth"
dotnet build .\ModDevelopment\Mods\Musketeers\Musketeers.csproj -c Release "-p:GameDirectory=$gameDirectory"
```

项目目标框架为 .NET Framework 3.5。Mod Menu 通过主项目引用启动补丁项目。编译输出位于各项目的 `bin/Release/net35`。

## 音效署名

火枪手音效改编自 J8054 的《musket_fire_9.wav》，原作采用 CC BY 4.0。安装文件夹里附有作者、来源及改动说明。[原始音效](https://freesound.org/people/J8054/sounds/648755/) · [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) · [完整署名](BepInEx/plugins/Musketeers/Musketeers-Audio-Credits.txt)
