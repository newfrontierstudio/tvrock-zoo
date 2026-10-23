param([switch]$Installer)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$artifacts = Join-Path $root 'artifacts'
$app = Join-Path $artifacts 'app'
New-Item -ItemType Directory -Path $app -Force|Out-Null
$total=[Diagnostics.Stopwatch]::StartNew()
$stage=[Diagnostics.Stopwatch]::StartNew()
dotnet publish (Join-Path $root 'src/App/TsScheduler.csproj') -c Release -r win-x64 --self-contained true -p:DebugType=None -p:DebugSymbols=false -p:RuntimeFrameworkVersion=8.0.32 -o $app
if ($LASTEXITCODE -ne 0) { throw 'Application build failed' }
$publishSeconds=$stage.Elapsed.TotalSeconds
$stage.Restart()
dotnet publish (Join-Path $root 'src/Recommend/TsRecommend.csproj') -c Release -r win-x64 --self-contained true -p:DebugType=None -p:DebugSymbols=false -p:RuntimeFrameworkVersion=8.0.32 -p:OutputType=WinExe -o (Join-Path $app 'Recommend')
if ($LASTEXITCODE -ne 0) { throw 'Recommendation build failed' }
$workerSeconds=$stage.Elapsed.TotalSeconds
$stage.Restart()
Copy-Item (Join-Path $root 'README.md') $app
Copy-Item (Join-Path $root 'LICENSE') $app
Copy-Item (Join-Path $root 'BENCHMARKS.md') $app
Copy-Item (Join-Path $root 'THIRD-PARTY-NOTICES.md') $app
if(!(Test-Path -LiteralPath (Join-Path $app 'licenses'))){Copy-Item (Join-Path $root 'licenses') $app -Recurse}
$copySeconds=$stage.Elapsed.TotalSeconds
$installerSeconds=0
if($Installer){
$stage.Restart()
$compiler = @("$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe", "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe") | Where-Object { Test-Path $_ } | Select-Object -First 1
if (!$compiler) { throw 'Install Inno Setup 6 first' }
& $compiler (Join-Path $root 'installer/TVRockZoo.iss')
if ($LASTEXITCODE -ne 0) { throw 'Installer build failed' }

$installerSeconds=$stage.Elapsed.TotalSeconds
}
[ordered]@{ApplicationPublishSeconds=$publishSeconds;WorkerPublishSeconds=$workerSeconds;SupportCopySeconds=$copySeconds;MediaCopySeconds=0;MediaDownloadSeconds=0;InstallerSeconds=$installerSeconds;TotalSeconds=$total.Elapsed.TotalSeconds}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $artifacts 'build-timing.json') -Encoding utf8
Write-Output (Join-Path $app 'TVRockZoo.exe')
