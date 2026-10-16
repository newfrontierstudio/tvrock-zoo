$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$artifacts = Join-Path $root 'artifacts'
$app = Join-Path $artifacts 'app'
dotnet publish (Join-Path $root 'src/App/TsScheduler.csproj') -c Release -r win-x64 --self-contained true -p:RuntimeFrameworkVersion=8.0.32 -o $app
if ($LASTEXITCODE -ne 0) { throw 'Application build failed' }
dotnet publish (Join-Path $root 'src/Recommend/TsRecommend.csproj') -c Release -r win-x64 --self-contained true -p:RuntimeFrameworkVersion=8.0.32 -p:OutputType=WinExe -o (Join-Path $app 'Recommend')
if ($LASTEXITCODE -ne 0) { throw 'Recommendation build failed' }
Copy-Item (Join-Path $root 'README.md') $app
Copy-Item (Join-Path $root 'THIRD-PARTY-NOTICES.md') $app
Copy-Item (Join-Path $root 'licenses') $app -Recurse -Force
$compiler = @("$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe", "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe") | Where-Object { Test-Path $_ } | Select-Object -First 1
if (!$compiler) { throw 'Install Inno Setup 6 first' }
& $compiler (Join-Path $root 'installer/TVRockZoo.iss')
if ($LASTEXITCODE -ne 0) { throw 'Installer build failed' }
