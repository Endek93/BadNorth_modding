# 从源码构建 / Build from source

这里包含六个 Mod 的源码和共用构建配置。从仓库根目录执行以下命令，并将游戏路径改成你的 Windows 安装位置。

~~~powershell
$gameDirectory = 'D:\SteamLibrary\steamapps\common\BadNorth'
dotnet build .\ModDevelopment\Mods\Musketeers\Musketeers.csproj -c Release "-p:GameDirectory=$gameDirectory"
~~~

需要 .NET SDK、已安装的《坏北》以及 BepInEx 5.4.23.6（x86）。项目以 .NET Framework 3.5 为目标，游戏程序集和 BepInEx 从本机安装目录引用。

- [Mods](Mods/) 下每个 Mod 有独立项目目录。替换示例中的项目路径即可构建其他 Mod。
- [Build/GameReferences.props](Build/GameReferences.props) 保存共用程序集引用。
- Mod Menu 的[主插件项目](Mods/ModMenu/Plugin/ModMenu.csproj) 会一并构建[启动补丁](Mods/ModMenu/Bootstrap/)。两者各自的 DLL 位于对应项目的 `bin/Release/net35` 目录。

想直接安装可以返回[单独下载入口](../README.md)。
