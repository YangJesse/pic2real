$projectRoot = Split-Path -Parent $PSScriptRoot
$playerPath = Join-Path $projectRoot 'Builds\Windows\DashedDemo.exe'
$outputPath = Join-Path $projectRoot 'TestResults\Runtime'
$logPath = Join-Path $projectRoot 'TestResults\player-verification.log'
if (!(Test-Path -LiteralPath $playerPath)) { throw 'Build the Windows player first.' }
New-Item -ItemType Directory -Force -Path $outputPath | Out-Null
$arguments = @('--verify-demo', '--verification-output', ('"' + $outputPath + '"'), '-force-d3d11', '-screen-fullscreen', '0', '-screen-width', '1600', '-screen-height', '900', '-logFile', ('"' + $logPath + '"'))
# Visual verification opens the interactive game window so Unity renders its back buffer.
$process = Start-Process -FilePath $playerPath -ArgumentList $arguments -PassThru
$process.WaitForExit()
if ($process.ExitCode -ne 0) { throw "Runtime verification failed. Read $logPath" }
Get-Content -LiteralPath (Join-Path $outputPath 'runtime-result.json')
