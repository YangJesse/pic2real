# demo1 照片成境

本目录是独立 Unity 项目。设计思路、参考作品、关卡与操作说明见[仓库 README](../README.md)。当前是粗糙原型，场景、节奏、玩法和画面均待优化。

## 开发环境

- Unity 6.3 LTS，版本 `6000.3.16f1`。
- URP `17.3.0`，依赖记录在 `Packages/manifest.json` 和 `packages-lock.json`。
- 当前已在 macOS Apple Silicon 环境构建和验证。其他平台尚未验证。

用 Unity Hub 打开本目录，载入 `Assets/Scenes/PhotoRealm.unity` 后点击 Play。运行时会生成完整建筑；不需要导入其他原型工程。

## Mac 构建

在 Unity 菜单中选择 **Photo Realm → Build playable Mac app**。构建输出为 `Builds/PhotoRealm.app`，当前配置为 ARM64。

也可以从项目目录运行：

```sh
/Applications/Unity/Hub/Editor/6000.3.16f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -projectPath "$PWD" \
  -executeMethod PhotoRealmBuild.BuildMac \
  -logFile /tmp/photo-realm-build.log
```

构建脚本会设置此项目的渲染和应用参数，并重新生成启动场景。关卡修改应在源码中进行；直接在自动生成场景中添加的临时内容可能在下一次构建时被替换。

## 主要源码

| 文件 | 内容 |
| --- | --- |
| `Assets/Scripts/PhotoRealmWorld.cs` | 建筑布局、照片部件、材质和环境细节 |
| `Assets/Scripts/PhotoRealmGame.cs` | 第一人称控制、照片交互、关卡状态、风力、排水和检查点 |
| `Assets/Scripts/PhotoRealmWater.shader` | 水面表现 |
| `Assets/Scripts/PhotoRealmLightShaft.shader` | 射灯可见光束 |
| `Assets/Scripts/PhotoRealmVerification.cs` | 自动通关与交互验证 |
| `Assets/Editor/PhotoRealmBuild.cs` | 场景生成及 Mac 构建 |
| `Assets/Editor/PhotoRealmRenderSetup.cs` | 独立的 URP 和后处理配置 |

## 验证方式

```sh
'./Builds/PhotoRealm.app/Contents/MacOS/Photo Realm' \
  --verify-photo-realm \
  -screen-width 1440 -screen-height 900 -screen-fullscreen 0 \
  -logFile /tmp/photo-realm-playtest.log
```

验证使用与键盘行走相同的控制器和交互方法，实际走完整条路线。开始前会主动制造一次坠落，确认安全点恢复；随后的路线不使用传送跳过路径。验证结束自动退出，结果保存在 `artifacts/photo-realm-qa/report.json`。

普通窗口验证会保存静态检查截图。`-batchmode` 可以执行逻辑验证，但不能保存屏幕截图。本项目当前没有录制视频。

独立工程已完成一次干净导入和 Mac 构建，构建为零错误、零警告；54 项自动通关检查全部通过。记录见[独立工程通关报告](../docs/validation/demo1-playtest.json)和[构建结果](../docs/validation/demo1-build.txt)。整理前的[来源版本报告](../docs/validation/demo1-source-playtest.json)也保留用于追溯。

## 素材说明

建筑和装置由项目代码生成。石材、木材和金属纹理由本地既有项目复用。中文字体为 Noto Sans CJK SC，随附的授权文件位于 `Assets/Fonts/OFL.txt`。

仓库不包含《Viewfinder》的模型、贴图或程序代码；其照片改变空间的玩法概念作为设计参考。
