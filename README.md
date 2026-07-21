# Rooftop Runner Demo

一个用于玩法验证和作品集展示的 Unity 3D 第三人称无尽跑酷极简 Demo。

当前版本目标是跑通最小闭环：开始游戏、自动前进、三车道切换、障碍生成、碰撞失败、距离计分、结算和一键重开。

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
- `R` / `Space`: 失败后重开

## 目录结构

```text
Runner/
├── Assets/
│   ├── Scenes/
│   │   └── SampleScene.unity              # Demo 入口场景
│   ├── Scripts/
│   │   ├── EndlessRunnerGame.cs           # 核心玩法逻辑
│   │   └── Runner.Runtime.asmdef          # 运行时程序集定义
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
- 三车道输入和插值移动
- 自动前进和距离计分
- 前方世界生成和身后对象清理
- 简单坐标碰撞判定
- 固定斜角第三人称相机
- IMGUI 开始/结算界面
- 最高分本地保存

### `Assets/Editor/RunnerBuild.cs`

Unity Editor 构建工具脚本。

可通过 Unity 菜单构建：

```text
Runner -> Build -> Mac Development
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
Builds/RooftopRunnerDemo.app
```

### `Assets/Tests/PlayMode/EndlessRunnerSmokeTests.cs`

PlayMode 冒烟测试，用于验证运行时能创建游戏控制器、玩家、主相机、世界根节点和基础可见几何体。

## 构建

### Unity 界面构建

推荐使用统一菜单入口：

```text
Runner -> Build -> Mac Development
```

该菜单会使用和命令行相同的构建逻辑。

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

```bash
/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -nographics \
  -quit \
  -projectPath /Users/lucas.l/Workspace/code/Runner \
  -executeMethod RunnerBuild.BuildMacDevelopment \
  -logFile /Users/lucas.l/Workspace/code/Runner/unity-build.log
```

运行构建产物：

```bash
open /Users/lucas.l/Workspace/code/Runner/Builds/RooftopRunnerDemo.app
```

以窗口模式运行：

```bash
open -n /Users/lucas.l/Workspace/code/Runner/Builds/RooftopRunnerDemo.app --args \
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

如果命令行提示项目已被另一个 Unity 实例打开，需要先关闭 Unity Editor，再重试。

## Unity 概念对应

- Scene: `SampleScene.unity`，游戏入口。
- GameObject: 场景中的对象，例如 `Runner`、道路、障碍、建筑。
- Component: 挂在 GameObject 上的功能模块，`EndlessRunnerGame` 就是脚本组件。
- MonoBehaviour: Unity 脚本基类，提供 `Awake`、`Update`、`OnGUI` 等生命周期。
- Transform: 控制对象位置、旋转、缩放。三车道切换本质上是改变玩家的 `x` 坐标。
- Material: 控制物体颜色和表面显示效果。当前 Demo 的材质都由代码创建。
- BuildPipeline: Unity Editor 构建 API，当前由 `RunnerBuild.cs` 调用。
- asmdef: 程序集定义文件，用于组织运行时代码和测试代码。

## 当前实现特点

这个 Demo 采用运行时生成场景的方式，不依赖外部模型资源。道路、障碍、城市建筑和角色都由基础 Primitive 动态创建。

这种方式适合快速验证玩法；后续如果继续扩展，可以逐步拆分为角色控制、世界生成、UI、计分、状态机等独立模块，并引入 Prefab 和正式 UI。
