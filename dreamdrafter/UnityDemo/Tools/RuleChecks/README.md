# 纯 C# 规则验收

此目录归档原先在系统临时目录执行的 87 项测试，未新增测试范围。测试直接编译工程内的 `Assets/Scripts/DemoRules.cs`，无需启动 Unity、下载包或连接网络。

双击 `Run.cmd`，或在 UnityDemo 工程根目录的 PowerShell 中执行：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\Tools\RuleChecks\Run.ps1"
```

默认使用 Windows 自带 .NET Framework 64 位 C# 编译器。非默认安装可传入 `-CompilerPath` 指定 `csc.exe`。

`RuleChecks.cs` 是完整测试源码；执行时生成 `RuleChecks.exe` 和 `result.txt`，后者记录规则源码与测试源码的 SHA256。这些运行产物不随仓库提交。每次执行都会重新编译，避免沿用旧规则；保留测试源码不代表本次提交已经重新执行全部测试。

覆盖比萨回弯、逐层承重与钟室；柱距和垂直边界、至少两轮八柱、16 旧鼓唯一身份与九根额枋；万神殿先落雨、排水平衡和太阳；科隆镂空与原物吊索分支；圣家堂相邻净高、碎片独占与局部重画；严格取景边界、无结构判定终页、进度保存往返及截断拒绝。Unity 场景主线与屏幕画面另由 `Assets/Scripts/DemoVerification.cs` 验收。
