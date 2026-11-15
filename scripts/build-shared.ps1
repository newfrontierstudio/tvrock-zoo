param([switch]$Installer,[string]$OutputRoot='',[string]$QuantumWorkerPath='')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$output=if($OutputRoot){[IO.Path]::GetFullPath($OutputRoot)}else{Join-Path $root 'artifacts'}
$app=Join-Path $output 'app'
if(Test-Path -LiteralPath $app){throw 'Use a fresh output folder; existing package is preserved.'}
if(!$QuantumWorkerPath){
 cargo build --release --locked --manifest-path (Join-Path $root 'src/QuantumWorker/Cargo.toml')
 if($LASTEXITCODE -ne 0){throw 'Rust build failed'}
 $QuantumWorkerPath=Join-Path $root 'src/QuantumWorker/target/release/quantum-core-rs.exe'
}
if(!(Test-Path -LiteralPath $QuantumWorkerPath)){throw 'Rust worker is missing'}
New-Item -ItemType Directory -Path $app -Force | Out-Null
foreach($target in @(@('src/App/TsScheduler.csproj','Studio'),@('src/Recommend/TsRecommend.csproj','Recommend'))){
 $stage=Join-Path $output ('publish-'+$target[1])
 dotnet publish (Join-Path $root $target[0]) -c Release -r win-x64 --self-contained true -p:RuntimeFrameworkVersion=8.0.32 -p:PublishSingleFile=false -p:PublishReadyToRun=false -p:DebugType=None -p:DebugSymbols=false -o $stage --nologo -v quiet
 if($LASTEXITCODE -ne 0){throw "Publish failed: $($target[1])"}
 foreach($file in Get-ChildItem -LiteralPath $stage -File -Recurse){
  if($file.Extension -in '.pdb','.xml'){continue}
  $relative=[IO.Path]::GetRelativePath($stage,$file.FullName);$destination=Join-Path $app $relative
  if(Test-Path -LiteralPath $destination){if((Get-FileHash $file.FullName).Hash -ne (Get-FileHash $destination).Hash){throw "Shared file mismatch: $relative"}}
  else{New-Item -ItemType Directory -Path (Split-Path $destination) -Force | Out-Null;Copy-Item -LiteralPath $file.FullName -Destination $destination}
 }
}
Copy-Item -LiteralPath $QuantumWorkerPath -Destination (Join-Path $app 'quantum-core-rs.exe')
[IO.File]::WriteAllText((Join-Path $app 'quantum-engine.txt'),'compare',[Text.UTF8Encoding]::new($false))
foreach($file in @('README.md','LICENSE','BENCHMARKS.md','THIRD-PARTY-NOTICES.md')){Copy-Item -LiteralPath (Join-Path $root $file) -Destination $app}
Copy-Item -LiteralPath (Join-Path $root 'licenses') -Destination $app -Recurse
if(Get-ChildItem -LiteralPath $app -Filter 'inferno*' -Recurse){throw 'Inferno must not be bundled'}
if($Installer){
 $compiler=@("$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe","${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe")|Where-Object {Test-Path $_}|Select-Object -First 1
 if(!$compiler){throw 'Inno Setup 6 is required'}
 & $compiler "/DBuildRoot=$output" (Join-Path $root 'installer/TVRockZoo.iss')
 if($LASTEXITCODE -ne 0){throw 'Installer build failed'}
}
Write-Output "Shared-runtime package: $app"
