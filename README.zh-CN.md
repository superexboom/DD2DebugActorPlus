# DD2 Debug Actor Plus

[English README](README.md)

DD2 Debug Actor Plus 是一个用于《Darkest Dungeon II》的独立 BepInEx 5 角色检查和编辑工具。它替换并扩展了游戏内置的 actor debug editor，提供更易读的中文 UI、支持本地化的标签、可搜索列表和直接编辑工具。

本项目用于本地调试和 Mod 开发，不隶属于 Red Hook Studios。

## 功能

- 战斗内和战斗外都可以呼出。
- 在运行时 API 允许的范围内，检查和编辑角色、已装备技能、战斗道具、饰品、怪癖、Token、Buff、DOT、压力、生命值和相关状态。
- 尽量显示本地化名称和描述，并在名称旁显示内部 id。
- 对当前角色身上的 Token、Buff、DOT、怪癖提供当前状态视图，方便直接移除。
- 支持中文名称的拼音和首字母搜索。
- 相比内置 debug actor 面板，提供更大、可调整大小的中文界面。

## 快捷键

| 按键 | 操作 |
| --- | --- |
| `Alt+J` | 打开或关闭 Debug Actor Plus |

## 安装

把 release zip 解压到游戏目录。

预期目录结构：

```text
Darkest Dungeon II/
└─ BepInEx/
   └─ plugins/
      └─ DD2DebugActorPlus/
         └─ DD2DebugActorPlus.dll
```

## 环境要求

- 《Darkest Dungeon II》
- BepInEx 5.x，当前使用 BepInEx 5.4.23.5 测试
- Unity/Mono 版 BepInEx，不是 IL2CPP 版
- 能够构建 `net48` 的 .NET SDK 或构建工具
- 来自 `Darkest Dungeon II_Data/Managed` 的本地游戏程序集

## 构建

1. 将 `Directory.Build.props.example` 复制为 `Directory.Build.props`。
2. 设置 `BepInExDir` 和 `ManagedDir`。
3. 构建：

```powershell
dotnet build .\DebugActorPlus\DebugActorPlus.csproj -c Release
```

仓库刻意排除了游戏程序集、反编译游戏源码、导出资源、本地路径和构建产物。

## 注意事项

- 这是强力调试工具，很容易制造无效存档状态。
- 部分定义只有在游戏进入对应场景或 run 状态后才会加载。
- 如果其他 Mod 也修改同一个 debug actor UI，补丁顺序可能会影响行为。
