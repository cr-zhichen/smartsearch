---
name: Smart Search for macOS
description: 任务优先的原生 macOS 搜索工作台。
---

# Design System: Smart Search for macOS

## Overview

**Creative North Star: "Apple 原生 macOS 工作台"**

使用系统窗口、字体与控件建立熟悉感。Codex Tweaks 提供结构参考，Smart Search 保留自己的名称和图标。先呈现主要任务、必要状态与下一步行动，技术细节按需进入。

**Key Characteristics:** 原生、清楚、克制；真实状态；稳定留白。

范围为 `desktop/macos`，依据 `DesktopLayout.swift`、`ContentView.swift`、`SmartSearchDesktopApp.swift`。原生令牌保存在 sidecar 的 `extensions.native`。**视觉验收仍待实机完成**；本文记录源码设计基线。

## Colors

- **Primary:** 系统强调色 `Color.accentColor`；绿表示完成，橙表示进行中或提醒，红表示失败。
- **Neutral:** `Color(nsColor: .textBackgroundColor)` 承载内容，浅色为白色、深色随系统解析；正文使用默认前景，辅助信息使用 `.secondary`。
- 面板边界为 `.separatorColor.opacity(0.7)`；状态标签底色透明度为 `0.12`。

**The 语义颜色 Rule.** 保留系统 API，不以固定 Hex 替代；状态必须有文字依据。

## Typography

系统字体负责全部普通内容。页面标题用 `.title2.weight(.semibold)`，分组用 `.headline`，正文用 `.body`，补充信息用 `.callout`／`.caption`。JSON、路径和日志使用系统等宽字体；耗时使用 `.monospacedDigit()`。可读结果额外行距为 4 pt。

## Layout

几何单位均为 macOS pt。`DesktopMetrics`：内容最大宽度 920、页面留白 24、章节间距 24、面板留白 16、圆角 12。内容左对齐并纵向滚动。

窗口默认 1080 × 760，最小 920 × 620。主侧栏宽度为 196／220／244（最小／理想／最大），保留原生标题栏与工具栏。

| 工作区 | 结构 |
| --- | --- |
| 概览 | 基础状态、下一步与能力；详情 sheet |
| 服务商 | 列表进入二级编辑；连接／高级分段选择 |
| 搜索与研究 | 输入／结果分段选择；选项 sheet |
| 活动 | 左列表、右详情；原生可调整分栏 |
| 更新 Skills | 目标开关列表；文件／偏好／环境 sheet |
| 设置与关于 | 通用／App 更新／独立 CLI／高级四类 |

## Elevation & Depth

内容保持平整，无自定义投影。依靠留白、文字和分隔建立层次；sheet 与 popover 使用系统浮层。

## Shapes

面板采用连续圆角及 1 pt 细边界；状态用 Capsule。按钮、输入和开关沿用原生形状。普通 GroupBox 只组织标题与内容。

## Components

- **Buttons:** 当前主要动作使用 `.borderedProminent`；运行中显示进度与文字，冲突时禁用。危险动作声明对应 role。
- **Inputs:** 字段按 metadata 使用 TextField／SecureField 或 Picker；现有 Toggle 使用 `.switch`。保留系统焦点与键盘交互，字段来源等详情进入 popover。
- **Navigation:** `NavigationSplitView` 与 sidebar；同级内容使用 segmented Picker。配置和 Skills 底部保留操作区。
- **Disclosure:** 整行使用真实 Button，最小高度 30，展开箭头旋转 90°；动画 `easeInOut(0.18s)`，减少动态效果开启时禁用。提供展开状态的可访问值。
- **Sheet:** 560 × 480，留白 20；标题、“完成”、分隔与滚动内容。
- **Result:** 正文和来源优先，脱敏 JSON 按需展开；文本可选取，复制和导出说明范围。

## Do's and Don'ts

### Do:

- **Do** 复用原生组件和统一间距，保持任务与返回上下文。
- **Do** 为状态提供文字、为图标提供名称，并验证浅深色、缩放和键盘焦点。

### Don't:

- **Don't** 用 CSS 仿制品、固定色值或额外阴影替代原生系统。
- **Don't** 让技术详情抢占主要任务，或将编译、打包视为视觉验收。
