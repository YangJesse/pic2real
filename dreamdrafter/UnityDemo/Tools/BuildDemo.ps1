param([string]$EditorPath = 'C:\Program Files\Unity\Hub\Editor\6000.6.5f1\Editor\Unity.exe')
$projectRoot = Split-Path -Parent $PSScriptRoot
if (!(Test-Path -LiteralPath $EditorPath)) { throw "Unity Editor not found: $EditorPath" }
$logPath = Join-Path $projectRoot 'TestResults\unity-build.log'
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $logPath) | Out-Null
$arguments = @('-batchmode', '-nographics', '-quit', '-projectPath', ('"' + $projectRoot + '"'), '-executeMethod', 'Dashed.Editor.DemoBuilder.SetupAndBuild', '-logFile', ('"' + $logPath + '"'))
$process = Start-Process -FilePath $EditorPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
$process.WaitForExit()
if ($process.ExitCode -ne 0) { throw "Unity build failed. Read $logPath" }
Write-Output (Join-Path $projectRoot 'Builds\Windows\DashedDemo.exe')
