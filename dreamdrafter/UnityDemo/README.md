# 梦筑师 / DreamDrafter — Unity 互动演示

在 **Unity Hub → Add / 添加本地项目** 中选择本目录 `dreamdrafter/UnityDemo`，使用 **Unity 6000.6.5f1** 打开。首次导入完成后，打开 `Assets/Scenes/00_Workshop.unity`，点击 **Play** 开始试玩。

这是供评审查看和运行的源工程，包含脚本、场景、游戏素材、项目配置及准备好的模型，不包含 Windows 可执行构建、Unity 缓存、测试日志、宣传视频或完整 Blender 制作资料。`打开演示.cmd` 仅在本地已构建 `Builds/Windows/DashedDemo.exe` 时启动播放器，否则显示编辑器打开方法。

本工程实现五个建筑场景，另有共用工作室的序章与晨光结局。使用本地预生成画与模型，运行时不需要联网、AI 密钥或 Blender。背景动画、年份快放、过场、角色配音属于后续制作内容；排水、挂链、原物归位等互动由程序驱动。部分游戏界面、Unity 菜单和代码命名仍保留原型工作名“虚线 / Dashed”，此交付版不改变原型游戏行为。

## 操作

| 输入 | 功能 |
| --- | --- |
| WASD / Shift | 行走 / 快走 |
| 按住鼠标右键 | 转头 |
| B / Tab | 打开或合起速写本 |
| F | 举页取景 |
| 左键 | 落笔；需站在固定取景点且误差小于 3° |
| E | 拾取、推柱鼓、看批注或推门 |
| F5 / F9 | 保存 / 读取进度 |
| H / Esc | 操作说明 / 合起当前界面 |

右侧操作按钮与键盘操作共用同一流程。“回到取景点 ×”方便演示时返回站位。三张硫酸纸各有预设参数，选中的参数进入结构判定；出错后可以换纸再落笔。没有自由提示词或任意绘画识别流程。

## 完整演示路线

1. 工作室：取本 → 横线搁板 → 取景落笔 → 拾 HB 铅笔和蜡笔半页 → 门框 → 推门。
2. 比萨：轴线笔 → 第二张硫酸纸 → 取景落笔 → 钟室挂正、七钟响起 → 门框。
3. 帕特农：推第一组柱鼓 → 五根柱 → 第二张纸 → 落笔；推第二组柱鼓 → 三根柱 → 落笔。旧柱鼓全部归位，额枋回到头顶。
4. 万神殿：橡皮擦顶 → 第二张纸 → 正午太阳 → 首次落笔只开洞落雨；连续画四个排水孔 → 水退、光照门槛 → 取出封底蜡笔半页，拼完整下一页 → 半石半蜡笔门框 → 进入科隆。
5. 科隆：带着已经拼完整的页 → 描南塔轮廓 → 橡皮擦塔尖至 52% → 选择蜡笔比例 → 落笔。可用吊索把花饰残片吊回塔尖 → 地面青铜门。
6. 圣家堂：三次擦镜清中央 → 三跨依次挂链并接住碎片，默认均选第二张 → 取景落笔进入中殿 → 终页只给一张 → 落笔显影荣耀立面 → 推门。
7. 晨光工作室：拿拱石描楔边 → 沿半径补桥 → 窗前取景落笔。新补半桥仅是玻璃上的画，侧移后产生视差 → 把描实的线说给外婆听。

## 工程结构与后续动画接入

五个建筑实际加载独立 Unity 场景：`01_Pisa`、`02_Parthenon`、`03_Pantheon`、`04_Cologne`、`05_Sagrada`。终页在圣家堂内，不另换场景。场景内的 `EditorStagePreview` 只供编辑器预览，运行时替换为互动构件。

- `Assets/Scripts/DemoDirector.cs`：笔刷动作、逐页状态、取景和场景切换。
- `Assets/Scripts/DemoWorld.cs`：交付模型实例、旧石归位、碰撞、挂链、镜中实时 RenderTexture 与门轴。
- `Assets/Scripts/DemoUI.cs`：速写本、预生成画、硫酸纸与交互提示。
- `Assets/Scripts/DemoRules.cs`：独立结构判定和可复用进度核心。
- `Assets/Resources/Models`：由真实交付网格生成的 Unity 原生 prefab，保留原节点名。
- `SourceModels`：准备好的 GLB 及共享纹理，供 Unity 编辑器重新导入；无需原 Blender 制作目录。
- `Tools/prepare_models.py`：从外部原始制作资料重建上述 GLB 的可选工具，普通试玩和构建不需要运行。
- `Assets/Editor/DemoBuilder.cs`：模型导入、场景生成与 Windows 构建菜单。

背景、音轨和 Timeline 可直接加入对应场景。需要挂到互动上的效果，调用或扩展 `Prepare`、`Capture`、`Drain`、`HangChain`、`ShowFinalFacade` 等动作；实际门轴、镜面与可替换模块已有单独对象。

Blender JSON 的 `(x,y,z)` 转为 Unity `(x,z,y)`，单位为米；GLB 已是 Y-up，只镜像交换格式的 Z 轴。圣家堂模型链的 1 m 下垂映射到中殿 10 m 净高，相邻跨允许 1.5 m 高差。链的端点、长度、荷载与页角数值由同一求解结果生成。

## 本地构建与检查

已有场景和原生 prefab 可直接运行，通常无需重新生成场景。Unity 菜单“虚线”提供模型导入、场景生成与 Windows 导出；重新生成场景会重写七个场景文件，应先保存自己的修改。导出 Windows 需要安装对应 Unity 版本的 Windows Build Support，也可在 Windows PowerShell 中从工程根目录运行：

```powershell
.\Tools\BuildDemo.ps1
# Editor 不在默认安装位置时，传入 -EditorPath 指定 Unity.exe。
```

`Tools/RuleChecks/Run.cmd` 可在 Windows 上重新执行独立 C# 结构规则检查。`Tools/VerifyDemo.ps1` 需要先构建 Windows Player，会打开游戏窗口执行互动流程检查并保存画面和报告到 `TestResults`。这些脚本和历史验收清单供复现参考；测试日志与截图未提交，历史记录不代表此发布副本已经重新执行全部检查。

## 可选：从原始制作资料重新准备素材

`Assets` 与 `SourceModels` 已随项目提供。下面的预处理仅面向持有原始 Blender 制作资料的维护者，评审试玩、代码阅读和 Unity 构建无需执行。

`prepare_models.py`、`prepare_art.py`、`prepare_col_surfaces.py` 与 `validate_models.py` 需要外部模型根目录，其直接子目录为 `比萨斜塔`、`帕特农`、`万神殿`、`科隆大教堂`、`圣家堂`，各场景下含 `Export`、`Preview` 等资料。可通过环境变量 `DREAMDRAFTER_MODEL_ROOT` 指定；未设置时依次查找工程同级的 `../模型`、`../交付`。本仓库不包含这个外部目录，缺失时工具会给出明确提示。

```powershell
$env:DREAMDRAFTER_MODEL_ROOT = 'D:\DreamDrafterSources\模型'
python .\Tools\prepare_models.py
python .\Tools\prepare_art.py
python .\Tools\prepare_col_surfaces.py
python .\Tools\validate_models.py
```

这些脚本需要 Python 3.10+，会更新项目中的准备素材和清单。`prepare_art.py` 保留随项目分发的 `Chinese.otf` 与 `OFL.txt`，不会复制操作系统字体。`validate_reimport.py` 是另一项可选 Blender 检查，只读取随仓库提供的 `SourceModels`，需要在 Blender 中执行。

详细策划对照见 `Documentation/策划需求与验收清单.md`（历史制作记录）；素材及开源字体来源见 `Documentation/AssetSources.txt`。
