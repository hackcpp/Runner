# Rooftop Runner

一个用于作品集展示的 Unity 3D 第三人称无尽跑酷切片，强调三车道决策、跳跃、滑铲和连续动作反馈。

产品名：`Rooftop Runner`

当前版本闭环为：开始游戏、自动前进、三车道切换、跳跃、滑铲、分级障碍模式、动作奖励、碰撞失败、结算和一键重开。

## 作品集预览

### 游戏中

![Rooftop Runner 游戏中画面](Portfolio/Screenshots/gameplay.png)

### 开始与暂停

| 开始 | 暂停 |
| --- | --- |
| ![Rooftop Runner 开始界面](Portfolio/Screenshots/start.png) | ![Rooftop Runner 暂停界面](Portfolio/Screenshots/pause.png) |

## 环境

- Unity: 2022.3.62f1c1
- 平台: macOS
- 输入: 键盘

## 运行

1. 用 Unity Hub 打开项目根目录：

   ```text
   /Users/lucas.l/Workspace/code/Runner
   ```

2. 打开场景：

   ```text
   Assets/Scenes/SampleScene.unity
   ```

3. 点击 Unity 顶部的 Play 按钮。

## 操作

- `Space` / `Return`: 开始游戏
- `A` / `Left Arrow`: 向左切换一条车道
- `D` / `Right Arrow`: 向右切换一条车道
- `Space` / `W` / `Up Arrow`: 跳跃
- `S` / `Down Arrow`: 滑铲
- `Esc` / `P`: 暂停或继续
- `R` / `Space`: 失败后重开

## 目录结构

```text
Runner/
├── Assets/
│   ├── Scenes/
│   │   └── SampleScene.unity              # 项目入口场景
│   ├── Scripts/
│   │   ├── EndlessRunnerGame.cs           # 游戏流程、生成、计分和 UI
│   │   ├── RunnerMotor.cs                 # 三车道、跳跃和滑铲运动
│   │   ├── RunnerGameplay.cs              # 障碍规则、模式目录、奖励和分数
│   │   ├── RunnerRunSimulation.cs         # 完整跑局生成和生存路径模拟
│   │   ├── RunnerHud.cs                   # 响应式 Canvas HUD 和状态面板
│   │   ├── RunnerCameraRig.cs             # 相机跟随、倾斜、FOV 和震动
│   │   ├── RunnerWorldPool.cs             # 世界几何体和障碍根节点复用
│   │   ├── ProceduralRunnerMusic.cs       # 程序化背景音乐
│   │   ├── ProceduralRunnerSfx.cs         # 程序化动作和碰撞音效
│   │   └── Runner.Runtime.asmdef          # 运行时程序集定义
│   ├── Brand/
│   │   └── AppIcon.png                    # macOS/TapTap 包体图标源图
│   ├── Editor/
│   │   └── RunnerBuild.cs                 # Editor 菜单和命令行构建脚本
│   └── Tests/
│       └── PlayMode/
│           ├── EndlessRunnerSmokeTests.cs # PlayMode 冒烟测试
│           └── Runner.PlayModeTests.asmdef
├── Packages/                              # Unity 包依赖配置
├── ProjectSettings/                       # Unity 项目设置
├── Builds/                                # 本地构建产物，默认不提交
└── README.md
```

## 主要文件

### `Assets/Scenes/SampleScene.unity`

项目入口场景。场景中包含主相机、方向光，以及显式挂载 `EndlessRunnerGame` 的 `Endless Runner Game` 对象。

显式挂载脚本可以保证 Editor Play 和打包后的 Player 都稳定初始化游戏。

### `Assets/Scripts/EndlessRunnerGame.cs`

游戏主体脚本，负责：

- 游戏状态：开始、游戏中、失败
- 运行时创建角色、道路、城市背景和障碍
- 三车道输入、跳跃、滑铲和插值移动
- 自动前进、距离计分、动作奖励和限时连击倍率
- 教学障碍、分级障碍模式和固定种子生成
- 完整生存路径验证和强制动作间距校验
- 前方世界生成、身后对象清理和运行时几何体复用
- 纯坐标碰撞，不让视觉几何体进入 PhysX
- 按障碍类型区分的坐标碰撞判定
- 带换道倾斜的固定斜角第三人称相机
- 速度 FOV、动作提示、地面预警形状和得分反馈
- 响应式 Canvas 开始、暂停、HUD 和结算界面
- 最佳距离、最佳分和最佳连击本地保存

### `Assets/Scripts/RunnerMotor.cs`

独立的角色运动组件。使用自定义坐标逻辑，不依赖 Rigidbody，负责换道、跳跃、滑铲、姿态动画和逻辑碰撞体高度。支持落地前跳跃/滑铲缓冲和单步换道输入队列。

### `Assets/Scripts/RunnerGameplay.cs`

集中保存 `Blocker`、`Hurdle`、`Overhead` 三类障碍规则、连击计分器，以及 12 个按距离分级的可学习障碍模式。模式使用固定种子时可以复现；路径求解器会按最高速度验证换道距离和动作间距。

### `Assets/Scripts/RunnerRunSimulation.cs`

纯 C# 跑局模拟器。复用运行时速度、教学距离和难度分档参数，能够从中心车道开始验证教学序列、随机模式及模式边界的完整生存路径。批量测试会覆盖 5000 个固定种子和 1200 米距离。

### `Assets/Scripts/RunnerHud.cs`

运行时创建的响应式 Canvas HUD，负责分数、距离、最佳分、连击进度、动作提示，以及开始、暂停和结算状态面板。

### `Assets/Scripts/RunnerCameraRig.cs`

独立管理相机跟随、速度 FOV、动作脉冲、换道倾斜和碰撞震动，避免相机反馈状态继续堆积在游戏主控制器中。

### `Assets/Scripts/RunnerWorldPool.cs`

集中管理道路、城市和障碍视觉对象的创建与回收，同时保证程序化视觉几何体不携带启用的物理碰撞体。

### `Assets/Editor/RunnerBuild.cs`

Unity Editor 构建工具脚本。

可通过 Unity 菜单构建：

```text
Runner -> Build -> Mac Development
Runner -> Build -> Mac Release
Runner -> Build -> Mac Release Zip For TapTap
```

也可通过命令行构建：

```bash
/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -nographics \
  -quit \
  -projectPath /Users/lucas.l/Workspace/code/Runner \
  -executeMethod RunnerBuild.BuildMacDevelopment \
  -logFile /Users/lucas.l/Workspace/code/Runner/unity-build.log
```

默认输出：

```text
Builds/RooftopRunner.app
```

TapTap macOS 上传包默认输出：

```text
Builds/RooftopRunner-mac-0.1.0.zip
```

### `Assets/Tests/PlayMode/EndlessRunnerSmokeTests.cs`

PlayMode 冒烟测试，用于验证运行时能创建游戏控制器、玩家、主相机、世界根节点和基础可见几何体。

### `Assets/Brand/AppIcon.png`

应用图标源图。构建脚本会在 macOS Release 构建后把它转换成 `PlayerIcon.icns`，写入 `.app`，并重新做 ad-hoc 签名。

## 构建

### Unity 界面构建

推荐使用统一菜单入口。日常调试用：

```text
Runner -> Build -> Mac Development
```

准备发布用：

```text
Runner -> Build -> Mac Release
```

准备 TapTap PC 上传包用：

```text
Runner -> Build -> Mac Release Zip For TapTap
```

这些菜单和命令行入口使用同一套构建逻辑。

也可以使用 Unity 原生构建窗口：

```text
File -> Build Settings...
```

确认 `Scenes In Build` 中包含：

```text
Assets/Scenes/SampleScene.unity
```

然后选择 macOS 平台并点击 `Build` 或 `Build And Run`。

### 命令行构建

Development 构建：

```bash
/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -nographics \
  -quit \
  -projectPath /Users/lucas.l/Workspace/code/Runner \
  -executeMethod RunnerBuild.BuildMacDevelopment \
  -logFile /Users/lucas.l/Workspace/code/Runner/unity-build.log
```

Release 构建：

```bash
/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -nographics \
  -quit \
  -projectPath /Users/lucas.l/Workspace/code/Runner \
  -executeMethod RunnerBuild.BuildMacReleaseForCommandLine \
  -logFile /Users/lucas.l/Workspace/code/Runner/unity-build.log
```

Release 构建并生成 TapTap zip 包：

```bash
/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -nographics \
  -quit \
  -projectPath /Users/lucas.l/Workspace/code/Runner \
  -executeMethod RunnerBuild.BuildMacReleaseZipForTapTapCommandLine \
  -logFile /Users/lucas.l/Workspace/code/Runner/unity-build.log
```

运行构建产物：

```bash
open /Users/lucas.l/Workspace/code/Runner/Builds/RooftopRunner.app
```

以窗口模式运行：

```bash
open -n /Users/lucas.l/Workspace/code/Runner/Builds/RooftopRunner.app --args \
  -screen-fullscreen 0 \
  -screen-width 1280 \
  -screen-height 720 \
  -logFile /Users/lucas.l/Workspace/code/Runner/rooftop-player.log
```

## 测试

运行 PlayMode 测试：

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

当前 PlayMode 冒烟套件共 17 项，覆盖角色输入缓冲、暂停恢复、完整障碍路径求解、5000 个种子的跨模式跑局模拟、无物理碰撞体约束、连击计分、固定种子，以及约 10 分钟距离的世界对象池稳定性。

### 当前验证基线

- PlayMode：`17/17` 通过
- 跑局公平性：连续验证 `5000` 个固定种子，每局 `1200m`，无无解序列
- 长跑稳定性：约 `10` 分钟距离模拟后，活动几何体、障碍数量和对象池容量保持有界
- macOS Release：`x86_64 + arm64` 通用二进制，版本 `0.1.0`
- 签名：`codesign --verify --deep --strict` 通过
- 窗口验证：`1280x720` 与 `1440x900` 下完成开始、换道、暂停、恢复和结算烟测

如果命令行提示项目已被另一个 Unity 实例打开，需要先关闭 Unity Editor，再重试。

## TapTap macOS PC 项目侧适配

当前项目只准备 macOS PC 端包体，不包含 Android/iOS 发布流程，也不包含 TapTap 开发者账号注册和后台提交流程。

### 1. 生成 Release 上传包

Unity 菜单：

```text
Runner -> Build -> Mac Release Zip For TapTap
```

或命令行：

```bash
/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -nographics \
  -quit \
  -projectPath /Users/lucas.l/Workspace/code/Runner \
  -executeMethod RunnerBuild.BuildMacReleaseZipForTapTapCommandLine \
  -logFile /Users/lucas.l/Workspace/code/Runner/unity-build.log
```

生成文件：

```text
Builds/RooftopRunner-mac-0.1.0.zip
```

### 2. 本机验包

先运行 `.app`：

```bash
open /Users/lucas.l/Workspace/code/Runner/Builds/RooftopRunner.app
```

确认：

- 能看到开始界面
- `Space` 能开始游戏
- `A` / `D` 能切换车道
- 撞障碍后能进入结算
- `R` 能重开
- Release 包没有 Development Build 水印

### 3. 包体信息

项目侧已经生成可上传包：

```text
Builds/RooftopRunner-mac-0.1.0.zip
```

macOS bundle 内部启动项路径：

```text
RooftopRunner.app/Contents/MacOS/Rooftop Runner
```

应用标识：

```text
com.hackcpp.rooftoprunner
```

版本号：

```text
0.1.0
```

### 4. 待补项目素材

上 TapTap 前项目侧还建议补齐：

- 图标
- 启动画面或自定义 splash
- 游戏截图：开始界面、跑酷中、失败结算
- 隐私政策页面链接
- 正式 Release 版本说明
- Apple Developer ID 签名和 notarization，公开下载前建议补齐

### 5. 当前项目配置

构建脚本会写入：

```text
Product Name: Rooftop Runner
Company Name: hackcpp
Bundle Identifier: com.hackcpp.rooftoprunner
Version: 0.1.0
Icon: Assets/Brand/AppIcon.png
```

`ProjectSettings/EditorBuildSettings.asset` 中已加入入口场景：

```text
Assets/Scenes/SampleScene.unity
```

## Unity 概念对应

- Scene: `SampleScene.unity`，游戏入口。
- GameObject: 场景中的对象，例如 `Runner`、道路、障碍、建筑。
- Component: 挂在 GameObject 上的功能模块，`EndlessRunnerGame` 就是脚本组件。
- MonoBehaviour: Unity 脚本基类，提供 `Awake`、`Update`、`OnGUI` 等生命周期。
- Transform: 控制对象位置、旋转、缩放。三车道切换本质上是改变玩家的 `x` 坐标。
- Material: 控制物体颜色和表面显示效果。当前版本的材质都由代码创建。
- BuildPipeline: Unity Editor 构建 API，当前由 `RunnerBuild.cs` 调用。
- asmdef: 程序集定义文件，用于组织运行时代码和测试代码。

## 当前实现特点

这个项目采用运行时生成场景的方式，不依赖外部模型资源。道路、障碍、城市建筑和角色都由基础 Primitive 动态创建。

这种方式适合快速验证玩法；后续如果继续扩展，可以逐步拆分为角色控制、世界生成、UI、计分、状态机等独立模块，并引入 Prefab 和正式 UI。
