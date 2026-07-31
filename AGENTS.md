# Rooftop Runner Agent Guide

本文件适用于整个仓库。后续代理在修改代码、资源、测试、构建配置或文档前，必须先阅读本文件、`README.md`、`ROADMAP.md` 及其指向的当前版本路线图。

## 项目目标

Rooftop Runner 是一个用于作品集展示的 Unity 3D 三车道关卡跑酷切片。当前重点是稳定、清晰、可验证的核心玩法和作品集完成度，不追求大型商业游戏的功能数量。

当前基线：

- Unity：`2022.3.62f1c1`
- 渲染管线：Built-in Render Pipeline
- 目标平台：macOS
- 输入：键盘、旧 Input API
- 当前规划版本：`0.1.1`，用于收尾已归档的 `0.1.0`
- 工程包体基线：`0.1.0`，在 `0.1.1` 正式构建前统一提升版本号
- Bundle Identifier：`com.hackcpp.rooftoprunner`
- 许可证：Unity Personal
- 测试基线：PlayMode `26/26`
- 本地构建：Mac Debug，`x86_64 + arm64` Universal Binary，ad-hoc 签名
- 正式打包：仅按需构建 Android ARM64 Release APK，使用项目独立证书签名

除非用户明确扩大范围，不迁移到 Input System、URP、Rigidbody 控制、正式 UI 框架、iOS 或手柄。

## 开始任务前

1. 运行 `git status -sb`，确认现有修改和当前分支。
2. 将所有未知修改视为用户工作，不覆盖、不回滚、不重新格式化无关文件。
3. 优化类任务先阅读 `ROADMAP.md` 及其指向的当前版本路线图，把当前条目标记为 `进行中` 并确认完成标准；除非需要追溯决策、验收证据或提交 SHA，不读取归档路线图。
4. 直接在项目目录工作，不复制整个 Unity 项目作为日常实现方式。
5. 检查 Unity Editor 是否已经打开本项目。若已打开，不要再启动第二个命令行 Unity 实例。

项目根目录：

```text
/Users/lucas.l/Workspace/code/Runner
```

Unity 可执行文件：

```text
/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity
```

## 代码职责

### `Assets/Scripts/EndlessRunnerGame.cs`

只负责关卡状态、世界生命周期、输入协调、障碍实例化、生命与检查点结算、计分协调和其他组件装配。新增独立行为优先进入对应组件，不继续无边界扩大主控制器。

### `Assets/Scripts/RunnerMotor.cs`

负责三车道移动、跳跃、滑铲、输入缓冲、动作状态、姿态和逻辑碰撞体。必须保持自定义坐标运动，不引入 Rigidbody 或 CharacterController。

### `Assets/Scripts/RunnerTouchInput.cs`

负责旧 Input API 下的 Android 触摸采样、滑动方向判定、移动中即时触发和 UI 触摸过滤。只向 `RunnerMotor` 提交现有动作请求，不复制运动状态、缓冲或碰撞规则。

### `Assets/Scripts/RunnerGameplay.cs`

负责障碍规则、不可变模式定义、模式目录、动作奖励、连击和分数公式。运行时和测试应复用同一套规则，不复制常量。

### `Assets/Scripts/RunnerRunSimulation.cs`

负责固定种子跑局生成与完整生存路径验证。此文件应保持可确定、无场景依赖；任何会影响障碍生成或速度的改动，都必须同步修改模拟器。

### `Assets/Scripts/RunnerHud.cs`

负责响应式 Canvas HUD、开始、暂停和结算界面，以及 `Screen.safeArea` 和触控暂停按钮。保持 `1280x720` 与 `1440x900` 下文本不重叠，不重新引入 IMGUI。

### `Assets/Scripts/RunnerCameraRig.cs`

负责跟随、FOV、换道倾斜、落地反馈和碰撞震动。相机反馈不得改变运动或碰撞规则。

### `Assets/Scripts/RunnerWorldPool.cs`

负责程序化世界几何体和障碍根节点复用。新增重复生成的视觉对象应进入对象池；视觉几何体不得保留启用的 PhysX Collider。

### `Assets/Editor/RunnerBuild.cs`

是 Mac Debug 与 Android Release APK 的唯一统一入口。构建相关设置应在这里固化，保证菜单构建与命令行构建一致；日常开发只构建 Mac Debug，正式打包时才构建 Android APK。

## 玩法不变量

- 保持三车道、三关固定终点模式：`360m`、`520m`、`720m`。
- 每关开始有三条命，在 `1/3` 与 `2/3` 处设置检查点；受击扣一条命并停在结果界面，玩家确认后才回到最近检查点，生命耗尽才判定关卡失败。
- 关卡速度必须逐级提高：第 1 关 `9.4 -> 12.5`、第 2 关 `10.4 -> 14.5`、第 3 关 `11.4 -> 16.5`；速度曲线变动必须同步模拟器验证。
- 检查点恢复后保留至少 `16m` 无障碍反应区；终点后最多保留 `42m` 仅用于远景的障碍，越过目标线必须先完成结算。
- 过终点后先进入约 `1.6s` 减速庆祝；期间锁定输入、碰撞与计分，结束后才显示通关面板。
- 每关目标位置必须显示横跨三车道的棋盘格终点线，并在终点前后保持至少 `3m` 无障碍净空；终点线只属于池化视觉几何体，不得带 Collider 或改变结算距离。
- Mac Debug 开始界面允许用关卡按钮或数字键 `1`、`2`、`3` 直达指定关卡；Release 和移动端不得暴露该测试入口。
- `A/D` 和左右方向键换道。
- `Space/W/Up` 跳跃，`S/Down` 滑铲。
- 跳跃和滑铲期间允许换道。
- 滑铲期间不能跳跃，空中不能直接滑铲；保留落地输入缓冲。
- `Blocker` 只能换道，`Hurdle` 需要跳跃，`Overhead` 需要滑铲。
- 每关教学障碍保持在约 `28m`、`48m`、`70m`。
- 两次强制动作至少保留最高速度下 `0.9s` 的响应时间。
- 每个模式、模式边界与三关完整路径都必须存在可验证的生存路径。
- 固定种子必须复现完整障碍序列。
- 动作奖励只能结算一次；绕开动作障碍不获得动作奖励。
- 保持 `EndlessRunner.HighScore`、`EndlessRunner.BestScore` 和 `EndlessRunner.BestCombo` 的存档兼容性。

如果需求与这些不变量冲突，先在当前版本路线图中写明范围变化和新的验收标准，再实施。

## 视觉与资源规则

- 当前主要使用运行时基础几何体和程序化音频，不随意引入外部包或商业素材。
- 障碍必须同时依靠形状与颜色区分，不能只依靠颜色。
- 屋顶设备只放在安全视觉区域，不得侵入三条逻辑车道。
- 程序化重复物体必须池化，并移除 Collider。
- 保持屋顶黄昏主题、暖色窗户、深色屋面和高对比障碍的现有视觉语言。
- 三关环境依次为蓝灰黄昏、金色夕照和清晰可读的紫蓝夜幕；第三关不得用低照度或浓雾降低障碍可读性。
- 新截图必须是有意更新的作品集资产；临时截图和 Player 日志不得提交。
- 不修改应用图标、产品名、版本号、Bundle Identifier 或 TapTap 打包方式，除非用户明确要求。
- 正式应用图标与 TapTap 上传图标必须来自同一画稿，不得分别设计两套风格。两者的构图、图形、配色、比例和标记必须一致，只允许因平台要求产生文件格式、像素尺寸、色彩配置或透明通道差异。
- 用户确认新图标为正式版本后，必须在同一轮同步 `Assets/Brand/AppIcon.png`、本地 `Distribution/TapTap/StoreUpload/Icon/` 和对应可编辑源文件；保留 Unity `.meta` GUID，并重新验证 macOS 与 Android 构建图标。
- 任何会改变实际游戏画面、HUD、角色、障碍、世界、灯光、特效或相机构图的修改，都必须检查并按影响范围同步 `Portfolio/Screenshots/` 与本地 `Distribution/TapTap/StoreUpload/` 中的截图、视频和宣传图。
- `Distribution/` 虽然不提交 Git，但不是可省略的发布同步目标。若该目录在当前工作区缺失或本轮无法重生成材料，必须在当前版本路线图和最终回复中明确记录未同步项，不得将相关视觉迭代标记为全部完成。
- 同步视觉资产后，检查文件用途、尺寸、格式、透明通道和文件大小，并确认 README、发布清单及上传指南没有引用过期文件。

## Unity Personal 启动画面

Unity Personal 必须保留 Unity Splash Screen 和 Unity Logo。不得通过修改 Player 二进制绕过许可证限制。

Player 构建必须保持：

- Splash Screen 开启
- Unity Logo 开启
- `AnimationMode.Static`
- `LightOnDark` Logo
- 深蓝黑背景
- 无背景图片和模糊

这些设置由 `RunnerBuild.ApplySplashScreenSettings()` 固化。

## 编码约定

- 使用现有 C# 风格：四空格缩进、Allman 大括号、明确类型、简短英文标识符。
- 注释只解释非显然约束或原因，不复述代码。
- 优先复用现有组件、常量和规则类型，不创建重复状态源。
- 只在能减少真实复杂度或重复时增加抽象。
- 不做与当前任务无关的大规模重命名、格式化或程序集调整。
- 修改 Unity 资源时同时维护对应 `.meta` 文件；不要手工更换已有 GUID。
- 新增运行时代码放入 `Runner.Runtime` 程序集可见范围。
- 测试使用固定种子，避免依赖系统时间和帧率偶然性。

## 测试

任何玩法、生成、计分、碰撞、池化或 HUD 改动都应更新 PlayMode 测试。

完整 PlayMode 命令：

```bash
/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -nographics \
  -projectPath /Users/lucas.l/Workspace/code/Runner \
  -runTests \
  -testPlatform PlayMode \
  -testResults /Users/lucas.l/Workspace/code/Runner/unity-playmode-results.xml \
  -logFile /Users/lucas.l/Workspace/code/Runner/unity-playmode-test.log
```

PlayMode Test Runner 会在完成后自行退出。不要添加 `-quit`；脚本发生重新导入时，该参数可能让 Unity 在测试启动前退出且不生成结果 XML。

注意：同一项目不能同时被两个 Unity 实例打开。如果 Editor 已打开，使用现有 Editor 的 Test Runner，或在确认没有未保存场景后再关闭 Editor 运行命令行测试。

测试完成后必须检查结构化结果，而不仅看进程退出码：

```bash
rg 'testcasecount|result="(Passed|Failed)"' unity-playmode-results.xml
```

当前重点覆盖：

- 运行时启动和程序化世界创建
- 背景音乐与独立一次性音效
- 教学障碍
- 跳跃、落地、滑铲和输入缓冲
- 程序化人物姿态、脚步节奏和动作粒子
- 三类障碍碰撞规则
- 单次动作奖励和连击
- 全模式合法路径和固定种子稳定性
- 确定性商店素材采集路径与模拟结果一致性
- 5000 个种子、1200 米基准跑局与每关完整跑局
- 多次关卡重置下的对象池有界性
- 暂停和恢复
- 四向触摸手势、触控流程按钮、安全区域和 Android 返回键
- 屋顶视觉几何体无 PhysX Collider

## 构建

日常开发只构建 Mac Debug：

```bash
/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -nographics \
  -quit \
  -projectPath /Users/lucas.l/Workspace/code/Runner \
  -executeMethod RunnerBuild.BuildMacDebug \
  -logFile /Users/lucas.l/Workspace/code/Runner/unity-build.log
```

正式打包或上架前才构建 Android Release APK：

```bash
/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -nographics \
  -quit \
  -projectPath /Users/lucas.l/Workspace/code/Runner \
  -executeMethod RunnerBuild.BuildAndroidReleaseForCommandLine \
  -logFile /Users/lucas.l/Workspace/code/Runner/unity-android-build.log
```

构建后至少验证：

```bash
file 'Builds/RooftopRunner.app/Contents/MacOS/Rooftop Runner'
/usr/bin/codesign --verify --deep --strict Builds/RooftopRunner.app
```

启动烟测：

```bash
'Builds/RooftopRunner.app/Contents/MacOS/Rooftop Runner' \
  -screen-width 1280 \
  -screen-height 720 \
  -screen-fullscreen 0 \
  -logFile rooftop-player.log
```

至少检查开始、输入、音乐、失败、重开、退出和 Player 日志；影响布局或视觉的改动还要检查 `1440x900`。

## 版本路线图

`ROADMAP.md` 是版本规划的唯一入口，只保存当前版本指针、归档索引和记录规则。具体工作记录在它指向的 `Roadmaps/<version>.md`；未来候选项记录在 `Roadmaps/BACKLOG.md`；已发布版本和旧历史保存在 `Roadmaps/Archive/`。

每轮工作必须：

1. 默认只阅读 `ROADMAP.md` 和当前版本路线图；只有需要追溯时才读取归档，只有规划下一版本时才读取 Backlog。
2. 活动版本与归档中的每条记录都使用 `ROADMAP.md` 定义的同一模板：状态、目标版本、完成日期、提交、设计目标、实现内容、验收标准、验收结果。
3. 开始前在活动版本路线图写清状态、设计目标、实现内容和验收标准。
4. 实现后记录自动化测试、构建、人工检查结果和未执行项。
5. 实现或文档内容先提交为 SHA `A`；全部验收标准满足后，将活动记录标记为 `已完成`，填写完成日期并把 `提交` 记为 `A`，再用后续提交保存路线图更新。
6. 新想法先进入 Backlog，不直接扩大活动版本。
7. 归档不得为了缩短文件而删除既有设计、实现或验收细节，也不得使用“当前迭代”“最近完成”等时态矛盾标题。
8. `提交` 字段只记录实现或内容提交，不记录回填路线图的元数据提交；不得尝试在文件中记录包含该文件的提交 SHA。
9. 活动版本保留该版本的全部状态记录；版本正式发布前确保归档范围内全部记录均为 `已完成`，再整体归档、创建下一版本路线图并更新入口指针。

## 生成物清理

每轮验证后清理：

- 根目录 `*.log`
- 测试结果 XML
- 临时截图日志
- 自动生成的 `*.csproj` 和 `*.sln`
- `.DS_Store`
- 已过期且不对应当前源码的 ZIP

默认保留最近一次验证通过的 `Builds/RooftopRunner.app`。

Unity Editor 打开期间不要删除 `Library`、`Temp`、`Logs` 或 `UserSettings`。只有在 Editor 完全退出，且需要排查导入异常或释放空间时才进行深度清理。

清理必须使用明确路径，优先移入废纸篓；不要用宽泛递归命令或未解析的环境变量。

## Git 规则

- 不提交 `Library`、`Temp`、`Logs`、`UserSettings`、`Builds`、日志、测试结果或 IDE 生成文件。
- 不使用 `git add -A` 暗中暂存未知修改；显式暂存本任务文件。
- 不回滚或覆盖用户已有修改。
- 未经明确要求不创建提交、不推送、不创建 PR。
- 用户要求直推 `main` 时不创建 PR，不使用 force push，并在推送前确认远端没有并发更新。
- 发布后校验远端 tree 与本地提交 tree 一致。
- 提交信息遵循 Conventional Commits，格式为 `<type>(<scope>): <description>`；不需要 scope 时可写为 `<type>: <description>`。
- `type` 使用小写标准类型：`feat`、`fix`、`docs`、`test`、`refactor`、`perf`、`build`、`ci`、`chore` 或 `revert`。
- `scope` 使用简短、稳定的小写模块名，例如 `gameplay`、`levels`、`android`、`taptap` 或 `roadmap`；不要把文件名或临时任务编号当作 scope。
- `description` 使用英文祈使语气，首字母小写，不以句号结尾，整行尽量不超过 72 个字符。
- 每个提交只包含一个逻辑关注点；代码、测试和直接相关文档应在同一提交中，独立的路线图 SHA 回填使用后续 `docs(roadmap)` 提交。
- 只有不兼容变更才使用 `<type>(<scope>)!:`，并在提交正文中增加 `BREAKING CHANGE: <说明>`；普通功能调整不得标记为破坏性变更。

## 完成检查

交付前按风险执行以下检查：

1. `git diff --check`
2. 检查 `git status -sb`，确认没有意外文件
3. 运行相关 PlayMode 测试；共享玩法或生成改动运行全量测试
4. 影响 Player、项目设置或资源的开发改动重建 Mac Debug；正式打包或上架前才构建 Android Release APK
5. 日常开发检查 Mac Debug 的 Universal Binary、签名和 Player 日志；正式打包时另查 APK 签名、包信息和哈希
6. 视觉或 HUD 改动实际检查两个窗口尺寸
7. 更新 `ROADMAP.md` 指向的当前版本路线图；只有版本切换、归档或规则变化时才修改入口文件
8. 清理本轮生成的日志、测试结果和过期构建输出

最终回复要说明改了什么、验证了什么、哪些内容未执行，以及当前修改是否已经提交。
