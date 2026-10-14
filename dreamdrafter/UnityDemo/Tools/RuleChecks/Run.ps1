param(
    [string]$CompilerPath = (Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe')
)

$ErrorActionPreference = 'Stop'
$taskProject = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$taskRules = Join-Path $taskProject 'Assets\Scripts\DemoRules.cs'
$taskChecks = Join-Path $PSScriptRoot 'RuleChecks.cs'
$taskExecutable = Join-Path $PSScriptRoot 'RuleChecks.exe'
$taskReport = Join-Path $PSScriptRoot 'result.txt'

if (!(Test-Path -LiteralPath $CompilerPath)) { throw "C# compiler not found: $CompilerPath" }
if (!(Test-Path -LiteralPath $taskRules)) { throw "Rules source not found: $taskRules" }

$taskCompileOutput = & $CompilerPath /nologo /target:exe "/out:$taskExecutable" $taskRules $taskChecks 2>&1
if ($LASTEXITCODE -ne 0) {
    [IO.File]::WriteAllText($taskReport, "FAIL: Compilation`r`n" + ($taskCompileOutput -join "`r`n"), [Text.UTF8Encoding]::new($false))
    throw ($taskCompileOutput -join "`r`n")
}

$taskOutput = & $taskExecutable 2>&1
$taskExitCode = $LASTEXITCODE
$taskHash = (Get-FileHash -LiteralPath $taskRules -Algorithm SHA256).Hash
$taskCheckHash = (Get-FileHash -LiteralPath $taskChecks -Algorithm SHA256).Hash
$taskText = @(
    'Pure C# structural rule checks for Dashed Unity Demo',
    'Rules source: Assets/Scripts/DemoRules.cs',
    "Rules SHA256: $taskHash",
    'Test source: Tools/RuleChecks/RuleChecks.cs',
    "Test SHA256: $taskCheckHash",
    "Compiler: $CompilerPath",
    "Exit code: $taskExitCode",
    '',
    ($taskOutput -join "`r`n")
) -join "`r`n"
[IO.File]::WriteAllText($taskReport, $taskText + "`r`n", [Text.UTF8Encoding]::new($false))
Write-Output $taskOutput
Write-Output "Report: $taskReport"
if ($taskExitCode -ne 0) { throw "Rule checks failed with exit code $taskExitCode" }
