# Rooftop Runner Agent Guide

本文件适用于整个仓库。后续代理在修改代码、资源、测试、构建配置或文档前，必须先阅读本文件、`README.md` 和 `OPTIMIZATION_ROADMAP.md`。

## 项目目标

Rooftop Runner 是一个用于作品集展示的 Unity 3D 三车道无尽跑酷切片。当前重点是稳定、清晰、可验证的核心玩法和作品集完成度，不追求大型商业游戏的功能数量。

当前基线：

- Unity：`2022.3.62f1c1`
- 渲染管线：Built-in Render Pipeline
- 目标平台：macOS
- 输入：键盘、旧 Input API
- 产品版本：`0.1.0`
- Bundle Identifier：`com.hackcpp.rooftoprunner`
- 许可证：Unity Personal
- 测试基线：PlayMode `17/17`
- Release：`x86_64 + arm64` Universal Binary，ad-hoc 签名

除非用户明确扩大范围，不迁移到 Input System、URP、Rigidbody 控制、正式 UI 框架、移动端或手柄。

## 开始任务前

1. 运行 `git status -sb`，确认现有修改和当前分支。
2. 将所有未知修改视为用户工作，不覆盖、不回滚、不重新格式化无关文件。
3. 优化类任务先阅读 `OPTIMIZATION_ROADMAP.md`，把当前条目标记为 `进行中` 并确认完成标准。
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

只负责游戏状态、世界生命周期、输入协调、障碍实例化、碰撞结算、计分协调和其他组件装配。新增独立行为优先进入对应组件，不继续无边界扩大主控制器。

### `Assets/Scripts/RunnerMotor.cs`

负责三车道移动、跳跃、滑铲、输入缓冲、动作状态、姿态和逻辑碰撞体。必须保持自定义坐标运动，不引入 Rigidbody 或 CharacterController。

### `Assets/Scripts/RunnerGameplay.cs`

负责障碍规则、不可变模式定义、模式目录、动作奖励、连击和分数公式。运行时和测试应复用同一套规则，不复制常量。

### `Assets/Scripts/RunnerRunSimulation.cs`

负责固定种子跑局生成与完整生存路径验证。此文件应保持可确定、无场景依赖；任何会影响障碍生成或速度的改动，都必须同步修改模拟器。

### `Assets/Scripts/RunnerHud.cs`

负责响应式 Canvas HUD、开始、暂停和结算界面。保持 `1280x720` 与 `1440x900` 下文本不重叠，不重新引入 IMGUI。

### `Assets/Scripts/RunnerCameraRig.cs`

负责跟随、FOV、换道倾斜、落地反馈和碰撞震动。相机反馈不得改变运动或碰撞规则。

### `Assets/Scripts/RunnerWorldPool.cs`

负责程序化世界几何体和障碍根节点复用。新增重复生成的视觉对象应进入对象池；视觉几何体不得保留启用的 PhysX Collider。

### `Assets/Editor/RunnerBuild.cs`

是 macOS 构建、图标、签名和 TapTap ZIP 的唯一统一入口。构建相关设置应在这里固化，保证菜单构建与命令行构建一致。

## 玩法不变量

- 保持三车道无尽模式。
- `A/D` 和左右方向键换道。
- `Space/W/Up` 跳跃，`S/Down` 滑铲。
- 跳跃和滑铲期间允许换道。
- 滑铲期间不能跳跃，空中不能直接滑铲；保留落地输入缓冲。
- `Blocker` 只能换道，`Hurdle` 需要跳跃，`Overhead` 需要滑铲。
- 开局教学障碍保持在约 `28m`、`48m`、`70m`。
- 两次强制动作至少保留最高速度下 `0.9s` 的响应时间。
- 每个模式和模式边界都必须存在可验证的生存路径。
- 固定种子必须复现完整障碍序列。
- 动作奖励只能结算一次；绕开动作障碍不获得动作奖励。
- 保持 `EndlessRunner.HighScore`、`EndlessRunner.BestScore` 和 `EndlessRunner.BestCombo` 的存档兼容性。

如果需求与这些不变量冲突，先在优化台账中写明范围变化和新的验收标准，再实施。

## 视觉与资源规则

- 当前主要使用运行时基础几何体和程序化音频，不随意引入外部包或商业素材。
- 障碍必须同时依靠形状与颜色区分，不能只依靠颜色。
- 屋顶设备只放在安全视觉区域，不得侵入三条逻辑车道。
- 程序化重复物体必须池化，并移除 Collider。
- 保持屋顶黄昏主题、暖色窗户、深色屋面和高对比障碍的现有视觉语言。
- 新截图必须是有意更新的作品集资产；临时截图和 Player 日志不得提交。
- 不修改应用图标、产品名、版本号、Bundle Identifier 或 TapTap 打包方式，除非用户明确要求。

## Unity Personal 启动画面

Unity Personal 必须保留 Unity Splash Screen 和 Unity Logo。不得通过修改 Player 二进制绕过许可证限制。

Release 构建必须保持：

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
  -quit \
  -projectPath /Users/lucas.l/Workspace/code/Runner \
  -runTests \
  -testPlatform PlayMode \
  -testResults /Users/lucas.l/Workspace/code/Runner/unity-playmode-results.xml \
  -logFile /Users/lucas.l/Workspace/code/Runner/unity-playmode-test.log
```

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
- 三类障碍碰撞规则
- 单次动作奖励和连击
- 全模式合法路径和固定种子稳定性
- 5000 个种子、每局 1200 米完整跑局
- 10 分钟对象池有界性
- 暂停和恢复
- 屋顶视觉几何体无 PhysX Collider

## macOS 构建

Release 命令：

```bash
/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -nographics \
  -quit \
  -projectPath /Users/lucas.l/Workspace/code/Runner \
  -executeMethod RunnerBuild.BuildMacReleaseForCommandLine \
  -logFile /Users/lucas.l/Workspace/code/Runner/unity-build.log
```

TapTap ZIP 命令：

```bash
/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -nographics \
  -quit \
  -projectPath /Users/lucas.l/Workspace/code/Runner \
  -executeMethod RunnerBuild.BuildMacReleaseZipForTapTapCommandLine \
  -logFile /Users/lucas.l/Workspace/code/Runner/unity-build.log
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

## 优化台账

`OPTIMIZATION_ROADMAP.md` 是优化状态的唯一入口。

每轮优化必须：

1. 开始前写清目标、状态和完成标准。
2. 实现后记录自动化测试、构建和人工检查结果。
3. 只有全部完成标准满足后才标记为 `已完成`。
4. 提交后补充提交 SHA。
5. 新想法先进入后续优先级，不直接扩大当前迭代。

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

## 完成检查

交付前按风险执行以下检查：

1. `git diff --check`
2. 检查 `git status -sb`，确认没有意外文件
3. 运行相关 PlayMode 测试；共享玩法或生成改动运行全量测试
4. 影响 Player、项目设置、资源或发布的改动重新构建 Release
5. 检查 Universal Binary、签名和 Player 日志
6. 视觉或 HUD 改动实际检查两个窗口尺寸
7. 更新 `OPTIMIZATION_ROADMAP.md`
8. 清理本轮生成的日志、测试结果和过期构建输出

最终回复要说明改了什么、验证了什么、哪些内容未执行，以及当前修改是否已经提交。
