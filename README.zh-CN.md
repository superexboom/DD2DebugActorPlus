# DD2 Debug Actor Plus

[English README](README.md)

DD2 Debug Actor Plus 是一个用于《Darkest Dungeon II》的独立 BepInEx 5 角色检查和编辑工具。它替换游戏内置的角色调试编辑器（Alt+J），改成游戏风格的编辑窗口：使用游戏自己的字体、图标和英雄头像，提供可搜索、带预览的库，以及英雄关系编辑。

本项目用于本地调试和 Mod 开发，不隶属于 Red Hook Studios。

## 功能

- 战斗内和战斗外都可以呼出。窗口是一个模态 uGUI 画布，文字用游戏自己的 TextMeshPro 字体；打开期间，鼠标点击、快捷键和 Esc 不会再穿透到游戏里。
- 左侧角色列：英雄和敌人竖排（滚轮滚动），每人显示头像、生命和压力；标签栏右侧显示当前正在编辑的角色。
- **生命 / 压力**：造成伤害、恢复生命、增减压力、设为指定数值，以及回满、清空等快捷按钮。
- **标记、增益、持续伤害、特质、物品**：每个页面都是同样的三栏布局：
  - 左栏：当前角色已经拥有的条目，每条都有"移除"按钮；点击条目会在库中定位；
  - 中栏：完整的库，带图标、分类筛选，支持按名称、ID、拼音和首字母搜索（虚拟列表，没有条数上限）；
  - 右栏：预览，显示游戏自己的描述，描述里的标记图标直接取自游戏的图标图集；以及添加和移除按钮。
  - 双击库中的条目直接添加，右键从角色身上移除。
- **关系**（恢复官方编辑器原有的功能，并重新设计）：
  - 按"对"展示（`A ⇄ B`）：游戏里每两名英雄只有一个和睦值和一种关系，由两人共用；
  - 当前选中的英雄始终显示在每张卡片的左边；可以在"与当前英雄相关"和"全部"之间切换；
  - 和睦值条按游戏的好感档位分段着色，提供 ±1、±5、最低、初始、最高按钮，并显示当前档位在下次从旅店出发时的基础判定概率；
  - **一键清除**当前关系；
  - **一键指定**：一条从最差到最好的刻度（憎恨……怀疑 · 中立 · 憧憬……爱恋）。点击后按游戏正常流程对两人同时生效（技能加成、插话、增益都会挂上），可选同时把和睦值调到对应档位。
- 中英双语界面。`auto` 跟随游戏语言，标题栏的按钮可以随时切换。

## 快捷键

| 按键 | 操作 |
| --- | --- |
| `Alt+J` | 打开或关闭 Debug Actor Plus（可在配置中修改） |
| `Esc` | 关闭编辑器（正在输入搜索时则先退出输入框） |

## 配置

`BepInEx/config/com.dd2.debugactorplus.cfg`

| 配置项 | 默认值 | 说明 |
| --- | --- | --- |
| `General.Language` | `auto` | `auto`（跟随游戏）、`zh`、`en` |
| `General.ToggleKey` | `J + LeftAlt` | 打开 / 关闭编辑器的快捷键 |
| `Appearance.UiScale` | `1` | 相对 1600×900 布局的窗口缩放（0.6–1.6）；默认值在 1280×720 下文字也能看清 |
| `Appearance.Frames` | 空 | 可选的边框覆盖，例如 `Panel=ui_tooltip@5; Button=ui_button_black@5`。可用角色：Window、Panel、Card、CardSelected、Button、ButtonPrimary、ButtonDanger、Chip、ChipSelected、Field、ScrollHandle、Divider；值为游戏贴图名加 `@` 边框缩小倍数，或 `none`。留空即使用内置的原生边框。改完在编辑器里点 **重新载入** 即可生效，不用重启游戏。 |
| `Diagnostics.SpriteGallery` | `false` | 增加一个"素材"页，把所有已加载的九宫格游戏贴图按边框样式和名字列出来，用来挑选 `Frames` |
| `Diagnostics.DumpUiSprites` | `false` | 首次打开编辑器时，同时把这些贴图名写进日志 |

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
- 关系只存在于一局游戏中、当前队伍里的英雄之间。非王国模式下，进入旅店时游戏会重置现有关系，再次出发时重新判定。
- 指定关系后游戏会像正常形成关系时一样立即存档。
- 边框、按钮、滚动条和分隔线都使用游戏自己的 UI 贴图（取自游戏从主菜单起就常驻的那些），字体、图标和头像也来自游戏本身。如果某张贴图缺失（例如游戏更新后改名），对应元素会自动退回绘制的边框。
- 如果其他 Mod 也修改同一个 debug actor UI，补丁顺序可能会影响行为。
