---
name: Smart Search for macOS
description: 任务优先的原生 macOS 搜索工作台。
---

# Design System: Smart Search for macOS

## Overview

**Creative North Star: "Apple 原生 macOS 工作台"**

使用系统窗口、字体与控件建立熟悉感。Codex Tweaks 提供结构参考，Smart Search 保留自己的名称和图标。先呈现主要任务、必要状态与下一步行动，技术细节按需进入。

**Key Characteristics:** 原生、清楚、克制；真实状态；稳定留白。

范围为 `desktop/macos`，依据 `DesktopLayout.swift`、`DesktopSplitView.swift`、`ContentView.swift`、`SmartSearchDesktopApp.swift`。原生令牌保存在 sidecar 的 `extensions.native`。搜索、服务商、活动的分栏及设置页紧凑布局已完成部分实机检查；英文、主题切换与宽窗口的视觉验收仍待完成。

## Colors

- **Primary:** 系统强调色 `Color.accentColor`；绿表示完成，橙表示进行中或提醒，红表示失败。
- **Neutral:** `Color(nsColor: .textBackgroundColor)` 承载内容，浅色为白色、深色随系统解析；正文使用默认前景，辅助信息使用 `.secondary`。
- 面板边界为 `.separatorColor.opacity(0.7)`；状态标签底色透明度为 `0.12`。

**The 语义颜色 Rule.** 保留系统 API，不以固定 Hex 替代；状态必须有文字依据。

## Typography

系统字体负责全部普通内容。页面标题用 `.title2.weight(.semibold)`，分组用 `.headline`，正文用 `.body`，补充信息用 `.callout`／`.caption`。JSON、路径和日志使用系统等宽字体；耗时使用 `.monospacedDigit()`。可读结果额外行距为 4 pt。

## Layout

几何单位均为 macOS pt。`DesktopMetrics`：内容最大宽度 920、页面留白 24、章节间距 24、面板留白 16、圆角 12。服务商编辑区留白 16，搜索输入 20、结果 24；分栏外不叠加页面留白。设置采用单一滚动内容区，最大宽度 800、页面留白 24、章节间距 24、标题与内容间距 12；紧凑面板使用水平留白 16、垂直留白 12、内容间距 8，标签与辅助说明间距 4。

窗口默认 1080 × 760，最小 920 × 620。主侧栏固定 220，可用原生工具栏按钮隐藏，保留 SwiftUI Scene 管理的标题栏和工具栏。内部分栏通过 `NSSplitViewController` 明确初始位置，并按页面保存调整后的宽度；内容切换只更新 hosting controller 内部视图。左侧 holding priority 为 251，右侧 250，均低于原生分隔条拖动优先级 490。窗口缩小时临时收窄，不覆盖用户偏好。

| 分栏 | 左栏最小／初始／最大 | 右栏最小 |
| --- | --- | --- |
| 服务商 | 184／220／280 | 400 |
| 搜索 | 280／320／360 | 360 |
| 活动 | 224／260／320 | 360 |

| 工作区 | 结构 |
| --- | --- |
| 概览 | 基础状态、下一步与能力；详情 sheet |
| 服务商 | 搜索框下方是统一滚动列表，意图路由为无图标的第一行，随列表滚动；随后按后端主要能力分组列出服务商，行内显示名称与配置状态，右侧副标题说明真实用途。路由模式使用本地化名称，仅展示当前模式参数，研究数据源偏好单独进入 |
| 搜索与研究 | 左侧输入、右侧结果；两栏分别滚动，选项使用 sheet |
| 活动 | 左列表、右详情；原生可调整分栏 |
| 更新 Skills | 目标开关列表；文件／偏好／环境 sheet |
| 设置与关于 | 顶部项目信息和 GitHub；同一页面依次呈现通用、App 更新、独立 CLI、高级设置，以标题、间距和有边框面板区分 |

## Elevation & Depth

内容保持平整，无自定义投影。依靠留白、文字和分隔建立层次；sheet 与 popover 使用系统浮层。

## Shapes

面板采用连续圆角及 1 pt 细边界；状态用 Capsule。按钮、输入和开关沿用原生形状。普通 GroupBox 只组织标题与内容。

## Components

- **Buttons:** 当前主要动作使用 `.borderedProminent`；运行中显示进度与文字，冲突时禁用。危险动作声明对应 role。
- **Settings rows:** 设置面板使用 `DesktopPanel(compact: true)`；说明靠左、控件靠右。自动更新开关保持原生尺寸，长说明允许换行；运行环境的说明和准备按钮位于同一行。
- **Inputs:** 字段按 metadata 使用 TextField／SecureField 或 Picker；现有 Toggle 使用 `.switch`。保留系统焦点与键盘交互，字段来源等详情进入 popover。
- **Secrets:** 已有 Key 显示固定圆点掩码，未配置时显示输入提示。掩码仅是 SecureField prompt，不进入草稿；有效值是否存在由后端布尔状态提供，空值和短 Key 不再靠掩码字符串判断。
- **Navigation:** `NavigationSplitView` 与 sidebar；记录筛选使用 segmented Picker；服务商、搜索及活动共用 `DesktopSplitView` 原生分栏。设置不增加第二套导航。配置和 Skills 底部保留操作区。
- **Providers:** 分组顺序为主搜索、文档检索、网页搜索、网页抓取、垂直检索；依据后端 `provider_profiles.capability`，不以必填字段或是否配置推断类型。多能力服务商只出现一次，详情说明全部声明能力和用途；实验性与显式调用限制保持可见。搜索覆盖名称、用途及全部能力，过滤不清除选择或草稿。
- **Disclosure:** 整行使用真实 Button，最小高度 30，展开箭头旋转 90°；动画 `easeInOut(0.18s)`，减少动态效果开启时禁用。提供展开状态的可访问值。
- **Language:** 语言变化时刷新列表、详情控件和菜单，保留外层 `NavigationSplitView` 的身份；页面选择留在稳定的父视图，草稿与搜索参数保留在 AppModel。运行标题从稳定 ID 重新取词。分栏 hosting controllers 明确传递 locale、colorScheme 和共用控件样式。
- **Sheet:** 560 × 480，留白 20；标题、“完成”、分隔与滚动内容。
- **Result:** 正文和来源优先，脱敏 JSON 按需展开；文本可选取，复制和导出说明范围。

## Do's and Don'ts

### Do:

- **Do** 复用原生组件和统一间距，保持任务与返回上下文。
- **Do** 为状态提供文字、为图标提供名称，并验证浅深色、缩放和键盘焦点。

### Don't:

- **Don't** 用 CSS 仿制品、固定色值或额外阴影替代原生系统。
- **Don't** 让技术详情抢占主要任务，或将编译、打包视为视觉验收。
