# Rooftop Runner

一个用于作品集展示的 Unity 3D 第三人称关卡跑酷切片，强调三车道决策、跳跃、滑铲、生命管理和连续动作反馈。

工程与 macOS 产品名：`Rooftop Runner`；TapTap Android 应用显示名：`天台疾跑`

当前版本闭环为：三关固定目标、逐关加速与复杂障碍、自动前进、三车道切换、跳跃、滑铲、动作奖励、三条命、检查点恢复、关卡庆祝和最终结算。

关卡节奏：第 1 关 `ROOFTOP BASICS` 为蓝灰黄昏，速度 `9.4 -> 12.5`；第 2 关 `CITY RHYTHM` 为金色夕照，速度 `10.4 -> 14.5`；第 3 关 `SUNSET SPRINT` 为清晰的紫蓝夜幕，速度 `11.4 -> 16.5`。每关在 `1/3` 与 `2/3` 保存检查点，恢复后有 `16m` 无障碍反应区；终点前会持续显示前方障碍，越过目标线优先结算。

已完成内容、当前迭代和后续优先级统一记录在 [优化台账](OPTIMIZATION_ROADMAP.md)。后续每轮优化都在该文档中更新实现状态与验收证据。

## 作品集预览

### 游戏中

![Rooftop Runner 游戏中画面](Portfolio/Screenshots/gameplay.png)

### 开始与暂停

| 开始 | 暂停 |
| --- | --- |
| ![Rooftop Runner 开始界面](Portfolio/Screenshots/start.png) | ![Rooftop Runner 暂停界面](Portfolio/Screenshots/pause.png) |

## 环境

- Unity: 2022.3.62f1c1
- 平台: macOS；Android APK 构建验证
- 输入: macOS 键盘；Android 横屏触摸手势

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
- `R` / `Space`: 生命耗尽后从最近检查点重试；通关后进入下一关
- 开始、暂停或结算界面的 `EXIT`: 退出游戏

Android 触屏：

- 点击界面按钮开始、继续、重开或退出
- 开始页会循环演示手指向左、右、上、下滑动的轨迹；教学障碍前显示当前需要的滑动方向
- 左右滑动切换车道，上滑跳跃，下滑滑铲；越过识别距离后立即响应，无需等待松手
- 点击右上角暂停按钮暂停；开始、暂停和结算界面提供 `EXIT`
- Android 返回键在跑动中暂停、在暂停界面继续、在开始或结算界面退出

## 目录结构

```text
Runner/
├── Assets/
│   ├── Scenes/
│   │   └── SampleScene.unity              # 项目入口场景
│   ├── Scripts/
│   │   ├── EndlessRunnerGame.cs           # 游戏流程、生成、计分和 UI
│   │   ├── RunnerMotor.cs                 # 三车道、跳跃和滑铲运动
│   │   ├── RunnerTouchInput.cs            # Android 手势识别与动作分发
│   │   ├── RunnerVisualRig.cs             # 程序化人物骨架和状态姿态
│   │   ├── RunnerMotionEffects.cs         # 脚步、尘雾、火花和速度拖尾
│   │   ├── RunnerGameplay.cs              # 障碍规则、模式目录、奖励和分数
│   │   ├── RunnerRunSimulation.cs         # 完整跑局生成和生存路径模拟
│   │   ├── RunnerMediaCapture.cs           # 显式参数启用的确定性商店素材采集
│   │   ├── RunnerHud.cs                   # 响应式 Canvas HUD 和状态面板
│   │   ├── RunnerCameraRig.cs             # 相机跟随、倾斜、FOV 和震动
│   │   ├── RunnerWorldPool.cs             # 世界几何体和障碍根节点复用
│   │   ├── ProceduralRunnerMusic.cs       # 分层程序化背景音乐与动态混音
│   │   ├── ProceduralRunnerSfx.cs         # 程序化动作和碰撞音效
│   │   └── Runner.Runtime.asmdef          # 运行时程序集定义
│   ├── Brand/
│   │   └── AppIcon.png                    # macOS/Android 包体图标源图
│   ├── Editor/
│   │   └── RunnerBuild.cs                 # macOS/Android 菜单和命令行构建脚本
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

- 游戏状态：开始、游戏中、暂停、关卡失败、关卡通关和全部完成
- 运行时创建角色、道路、城市背景和障碍
- 三车道输入、跳跃、滑铲和插值移动
- 自动前进、距离计分、动作奖励和限时连击倍率
- 三个固定距离关卡、三条命、两个检查点、逐关速度曲线、分级障碍模式和固定种子生成
- 每关完整生存路径验证和强制动作间距校验
- 前方世界生成、终点前瞻障碍、身后对象清理和运行时几何体复用
- 纯坐标碰撞，不让视觉几何体进入 PhysX
- 按障碍类型区分的坐标碰撞判定
- 带换道倾斜的固定斜角第三人称相机
- 速度 FOV、动作提示、地面预警形状和得分反馈
- 响应式 Canvas 开始、暂停、关卡进度、生命、检查点和通关结算界面
- 最佳距离、最佳分和最佳连击本地保存

### `Assets/Scripts/RunnerMotor.cs`

独立的角色运动组件。使用自定义坐标逻辑，不依赖 Rigidbody，负责换道、跳跃、滑铲、姿态动画和逻辑碰撞体高度。支持落地前跳跃/滑铲缓冲和单步换道输入队列。

### `Assets/Scripts/RunnerTouchInput.cs`

Android 触摸输入组件。按屏幕 DPI 计算最小滑动距离，通过主轴比例区分左右与上下手势，在移动中越过阈值时立即响应，忽略从 UI 控件开始的触摸，并将动作提交到 `RunnerMotor` 现有请求接口。键盘与触摸因此共用输入缓冲、换道队列和动作限制。

### `Assets/Scripts/RunnerVisualRig.cs`

程序化低多边形人物组件。运行时创建头、躯干、髋部和四肢，通过面罩与背部装饰明确朝向，并根据 `RunnerMotor` 状态表现跑步摆臂、换道侧倾、腾空、落地回弹和滑铲姿态。视觉部件不参与 PhysX，也不修改逻辑碰撞高度。

### `Assets/Scripts/RunnerMotionEffects.cs`

角色动作粒子组件。复用固定的尘雾、滑铲火花和速度拖尾粒子系统，配合脚步节奏和落地事件触发，不在跑动过程中持续创建 GameObject。

### `Assets/Scripts/RunnerGameplay.cs`

集中保存 `Blocker`、`Hurdle`、`Overhead` 三类障碍规则、连击计分器，以及 12 个按距离分级的可学习障碍模式。模式使用固定种子时可以复现；路径求解器会按最高速度验证换道距离和动作间距。

### `Assets/Scripts/RunnerRunSimulation.cs`

纯 C# 跑局模拟器。复用运行时每关速度曲线、教学距离、关卡目标和难度上限，能够从中心车道开始验证教学序列、随机模式、模式边界及三关完整生存路径，并为确定性素材采集暴露已验证的障碍行与车道路径。批量测试会覆盖 5000 个固定种子和 1200 米基准距离。

### `Assets/Scripts/RunnerMediaCapture.cs`

仅在 Player 显式传入 `-taptapCapture -captureOutput <目录>` 时启用。它使用固定种子和模拟器验证过的路径自动完成换道、跳跃与滑铲，严格遵守正常碰撞结算，以 `1920x1080`、24 fps 输出第 3 关连续帧，并采集第 1 关生命 HUD、第 2 关检查点恢复和第 3 关通关庆祝图；普通启动及 Android 触控流程不会挂载该组件。

### `Assets/Scripts/RunnerHud.cs`

运行时创建的响应式 Canvas HUD，负责关卡、进度、生命、分数、最佳分、连击进度、动作提示，以及开始、暂停、失败和通关状态面板。游戏信息、暂停按钮和面板内容会跟随 `Screen.safeArea`，避免侵入刘海、挖孔或圆角区域。

### `Assets/Scripts/RunnerCameraRig.cs`

独立管理相机跟随、速度 FOV、动作脉冲、换道倾斜和碰撞震动，避免相机反馈状态继续堆积在游戏主控制器中。

### `Assets/Scripts/RunnerWorldPool.cs`

集中管理道路、城市和障碍视觉对象的创建与回收，同时保证程序化视觉几何体不携带启用的物理碰撞体。

### `Assets/Scripts/ProceduralRunnerMusic.cs`

运行时一次性生成三条同步的 `126 BPM`、64 拍程序化音乐层：氛围、节奏和高强度驱动。菜单、低强度跑动、`400m+` 高强度跑动与结算状态通过 `0.72s` 交叉淡化切换；跳跃、滑铲、落地、得分和碰撞会触发约 `-3dB` 的短音乐闪避。重开、暂停和恢复只改变混音状态，不重新创建 `AudioSource` 或 `AudioClip`。

### `Assets/Editor/RunnerBuild.cs`

Unity Editor 构建工具脚本。

可通过 Unity 菜单构建：

```text
Runner -> Build -> Mac Debug
Runner -> Build -> Android Release APK
```

也可通过命令行构建：

```bash
/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -nographics \
  -quit \
  -projectPath /Users/lucas.l/Workspace/code/Runner \
  -executeMethod RunnerBuild.BuildMacDebug \
  -logFile /Users/lucas.l/Workspace/code/Runner/unity-build.log
```

默认输出：

```text
Builds/RooftopRunner.app
```

Android Release 默认输出：

```text
Builds/RooftopRunner-android-0.1.0.apk
```

### `Assets/Tests/PlayMode/EndlessRunnerSmokeTests.cs`

PlayMode 冒烟测试，用于验证运行时能创建游戏控制器、玩家、主相机、世界根节点和基础可见几何体。

### `Assets/Brand/AppIcon.png`

应用图标源图。构建脚本会在 Mac Debug 构建后把它转换成 `PlayerIcon.icns`，写入 `.app`，并重新做 ad-hoc 签名。

## 构建

### Unity 界面构建

推荐使用统一菜单入口。日常调试用：

```text
Runner -> Build -> Mac Debug
```

正式打包或上架前才构建 Android APK：

```text
Runner -> Build -> Android Release APK
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

Mac Debug 构建：

```bash
/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -nographics \
  -quit \
  -projectPath /Users/lucas.l/Workspace/code/Runner \
  -executeMethod RunnerBuild.BuildMacDebug \
  -logFile /Users/lucas.l/Workspace/code/Runner/unity-build.log
```

Android ARM64 Release APK（仅正式打包时运行）：

```bash
/Applications/Unity/Hub/Editor/2022.3.62f1/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -nographics \
  -quit \
  -projectPath /Users/lucas.l/Workspace/code/Runner \
  -executeMethod RunnerBuild.BuildAndroidReleaseForCommandLine \
  -logFile /Users/lucas.l/Workspace/code/Runner/unity-android-build.log
```

Android 构建配置：

```text
Package Name: com.hackcpp.rooftoprunner
Application Label: 天台疾跑
Version Name: 0.1.0
Version Code: 1
Minimum API: 24 (Android 7.0)
Target API: 35 (Android 15)
Scripting Backend: IL2CPP
Architecture: ARM64
Orientation: Landscape
Output: Builds/RooftopRunner-android-0.1.0.apk
```

Release APK 强制使用项目独立证书签名，不会回退到 Android Debug 证书。keystore 保存在本地忽略目录 `Distribution/Signing/RooftopRunner-release.keystore`，alias 为 `rooftoprunner`；密码从 macOS Keychain 服务 `RooftopRunner Android Release` 的 `storepass`、`keypass` 账户读取，也可分别通过 `ROOFTOP_RUNNER_ANDROID_STORE_PASS`、`ROOFTOP_RUNNER_ANDROID_KEY_PASS` 注入。证书 SHA-256 为 `3E:BC:01:D8:28:9F:5D:8C:AA:66:9F:F5:C8:AD:60:FA:6E:5D:6E:37:97:1F:D3:80:2E:C1:53:AF:2F:B8:A7:10`。

keystore 与 Keychain 凭据必须分别做安全的离机备份，丢失后无法为同一 Android 应用发布可升级版本。不要把 keystore、密码或 Keychain 导出提交到 Git。

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
  -projectPath /Users/lucas.l/Workspace/code/Runner \
  -runTests \
  -testPlatform PlayMode \
 -testResults /Users/lucas.l/Workspace/code/Runner/unity-playmode-results.xml \
 -logFile /Users/lucas.l/Workspace/code/Runner/unity-playmode-test.log
```

PlayMode Test Runner 会在测试结束后自行退出；不要为该命令添加 `-quit`，否则脚本重新导入时可能在测试启动前提前退出。

当前 PlayMode 冒烟套件共 25 项，覆盖角色输入缓冲、触摸手势分类与动作分发、安全区域、明确退出入口和纯触控流程、程序化人物姿态与动作粒子、分层音乐结构与样本质量、动态混音和音频对象复用、暂停恢复、三关固定种子完整路径、三条命与检查点恢复、关卡通关推进、无物理碰撞体约束、连击计分，以及重复关卡下的世界对象池稳定性。

### 当前验证基线

- PlayMode：`25/25` 通过
- 程序化音乐：三层同步循环约 `30.48s`，峰值、直流偏移、循环接缝和有限样本全部通过自动化阈值
- 音频稳定性：关卡重试、暂停、恢复及重复关卡运行后仍保持 4 个音源和 3 个音乐片段，不重复创建
- 跑局公平性：连续验证 `5000` 个固定种子、`1200m` 基准跑局和三关固定路径，无无解序列
- 关卡稳定性：重复第三关后，活动几何体、障碍数量和对象池容量保持有界
- Mac Debug：`x86_64 + arm64` 通用二进制，版本 `0.1.0`，用于日常本地测试
- Android Release：仅正式打包时生成；IL2CPP ARM64 APK，最低 API 24、目标 API 35，版本 `0.1.0`
- Android 触摸：四向滑动、触控流程按钮、安全区域、返回键和明确退出入口通过自动化及 ARM64 真机烟测
- 签名：Mac Debug 的 `codesign --verify --deep --strict` 与 Android Release APK v2 校验通过
- 窗口验证：`1280x720` 与 `1440x900` 下完成开始、换道、暂停、恢复、结算和退出烟测

背景音乐与动态编排已完成代码、自动化、Player 烟测，以及耳机和扬声器主观试听。

如果命令行提示项目已被另一个 Unity 实例打开，需要先关闭 Unity Editor，再重试。

## TapTap 发布准备

当前 TapTap 开发者后台提供 Android、TapTap Windows 和 Steam 配置，没有独立 macOS 包上传入口。因此当前上架主线使用 Android Release APK；Mac 只保留本地 Debug `.app`，不生成发布 ZIP。

商店名称、定位文案、上传素材、发布清单和隐私政策草案统一保存在本地 `Distribution/TapTap/`。根目录 `Distribution/` 是不提交到 GitHub 的发布工作区，已由 `.gitignore` 整体排除，因此 README 不链接其中的本地文件。

三类视觉资源的职责固定如下：

| 目录 | 唯一用途 | Git 状态 |
| --- | --- | --- |
| `Assets/Brand/` | Unity 构建实际使用的应用图标源图 | 提交 |
| `Portfolio/Screenshots/` | README 与 GitHub 作品集预览截图 | 提交 |
| `Distribution/TapTap/StoreUpload/` | TapTap 后台待上传的图标、截图、视频和宣传图导出件 | 不提交 |

`Assets/Brand/AppIcon.png` 与 TapTap 上传图标必须使用同一画稿；两者只允许因目标平台要求存在格式、尺寸、色彩配置或透明通道差异，不能使用不同构图或风格。

Android 已具备正式签名 APK 构建、横屏触控和至少一台 ARM64 真机验收能力，但正式上架仍需安全版本 Unity 重建、离机备份签名材料、隐私政策、资质与防沉迷材料；iOS 尚未适配。

### 1. 生成本地 Release 包

Android TapTap 候选包：

```text
Runner -> Build -> Android Release APK
```

默认输出：

```text
Builds/RooftopRunner-android-0.1.0.apk
```

当前 APK 已使用项目独立证书完成 v2 签名。正式上传前仍需升级到安全版本 Unity 后重新构建、核验签名并在真机回归。

### 2. macOS 本机验包

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
- Development Build 水印与调试功能符合本地测试用途

### 3. 当前包体信息

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

### 4. TapTap 上架阻塞项

后台正式发布前至少还需补齐：

- ISBN 与游戏资质；后台提示正式上线开放下载或内购时必须提供。
- 未成年人防沉迷系统和接入视频证明；后台提示 APK 测试或下载需要提供。
- Android 签名材料离机备份、公开 HTTPS 隐私政策和正确的平台、类型、包名配置。
- 已生成的 3 张 `1920x1080` 横屏实机截图、18 秒 H.264 视频和 `1920x1080` 标题宣传图需要在 Android 真机对照后手动上传。
- 隐私政策草案仍需填写发布主体、联系邮箱和生效日期，并部署为公开 HTTPS 页面。

完整尺寸、格式和渠道清单见本地文件 `Distribution/TapTap/MATERIAL_CHECKLIST.md`。

### 5. 当前项目配置

构建脚本会写入：

```text
Product Name: Rooftop Runner
Android Application Label: 天台疾跑
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
