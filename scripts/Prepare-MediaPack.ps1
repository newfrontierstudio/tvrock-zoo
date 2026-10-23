param([string]$Destination,[string]$Cache)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if(!$Destination){$Destination=Join-Path $root 'artifacts/media-pack/Tools'}
if(!$Cache){$Cache=Join-Path $root 'artifacts/media-cache'}
New-Item -ItemType Directory $Destination,$Cache -Force|Out-Null
$seven=@('C:/Program Files/7-Zip/7z.exe',(Get-Command 7z -ErrorAction SilentlyContinue).Source)|Where-Object {$_ -and (Test-Path -LiteralPath $_)}|Select-Object -First 1
if(!$seven){throw '7-Zip is required to prepare the media binaries at build time.'}
$packages=@(
 @{Name='ffmpeg';Url='https://github.com/shinchiro/mpv-winbuild-cmake/releases/download/20261021/ffmpeg-x86_64-git-9a861e968.7z';Hash='FC9C135B799B410FFC78517F428904C299BC64CD8144F21F63F84FCD8C35EA19'},
 @{Name='mpv';Url='https://github.com/shinchiro/mpv-winbuild-cmake/releases/download/20261021/mpv-x86_64-20261021-git-e471496c56.7z';Hash='4A355F3E89C83FABC730D33FB4E759DFEC747806ED2287B61A90D1681B83E8D3'},
 @{Name='ffmpeg-essentials';Url='https://www.gyan.dev/ffmpeg/builds/packages/ffmpeg-9.0.2-essentials_build.7z';Hash='4705843CCAAF54257C16AD90F3E952ECE33C17DF964ECF7BFDBB0F49C7171077'}
)
foreach($package in $packages){
 $file=Join-Path $Cache ($package.Name+'.7z')
 if(!(Test-Path -LiteralPath $file)){Invoke-WebRequest $package.Url -OutFile $file}
 if((Get-FileHash -LiteralPath $file).Hash -ne $package.Hash){throw "Media package hash mismatch: $($package.Name)"}
 $folder=Join-Path $Cache $package.Name
 & $seven x $file ('-o'+$folder) -y|Out-Null
 if($LASTEXITCODE -ne 0){throw "Extraction failed: $($package.Name)"}
}
Copy-Item -LiteralPath (Join-Path $Cache 'ffmpeg/ffmpeg.exe') -Destination $Destination -Force
Copy-Item -LiteralPath (Join-Path $Cache 'mpv/mpv.exe') -Destination $Destination -Force
Copy-Item -LiteralPath (Join-Path $Cache 'mpv/d3dcompiler_43.dll') -Destination $Destination -Force
$probe=Get-ChildItem (Join-Path $Cache 'ffmpeg-essentials') -Filter ffprobe.exe -File -Recurse|Select-Object -First 1
Copy-Item -LiteralPath $probe.FullName -Destination $Destination -Force
$licenses=Join-Path $Destination 'licenses';New-Item -ItemType Directory $licenses -Force|Out-Null
Copy-Item -LiteralPath (Join-Path $probe.Directory.Parent.FullName 'LICENSE') -Destination (Join-Path $licenses 'FFprobe-GPLv3.txt') -Force
Invoke-WebRequest 'https://raw.githubusercontent.com/FFmpeg/FFmpeg/9a861e968/COPYING.GPLv3' -OutFile (Join-Path $licenses 'FFmpeg-GPLv3.txt')
Invoke-WebRequest 'https://raw.githubusercontent.com/mpv-player/mpv/e471496c56/LICENSE.GPL' -OutFile (Join-Path $licenses 'mpv-GPL.txt')
@'
Media tools are separate unmodified executables. No codec library is linked into the application.
ffmpeg: shinchiro 20261021, FFmpeg commit 9a861e968.
mpv: shinchiro 20261021, mpv commit e471496c56 (FFmpeg based playback).
ffprobe: Gyan FFmpeg 9.0.2 essentials, GPLv3.
Corresponding source and build recipes:
https://github.com/FFmpeg/FFmpeg/tree/9a861e968
https://github.com/mpv-player/mpv/tree/e471496c56
https://github.com/shinchiro/mpv-winbuild-cmake/tree/20261021
https://www.gyan.dev/ffmpeg/builds/
https://ffmpeg.org/releases/ffmpeg-9.0.2.tar.xz
Redistribution archive URLs and SHA256 are pinned in Prepare-MediaPack.ps1.
The upstream source and build recipes describe the third-party codec dependencies and their licenses.
The GPL notices apply to the media tools, not the independently running Studio application.
'@|Set-Content (Join-Path $Destination 'SOURCES.txt')
Get-ChildItem -LiteralPath $Destination -File|Where-Object {$_.Name -ne 'SHA256.csv'}|ForEach-Object {[pscustomobject]@{Name=$_.Name;SHA256=(Get-FileHash -LiteralPath $_.FullName).Hash}}|Export-Csv (Join-Path $Destination 'SHA256.csv') -NoTypeInformation -Encoding utf8
Write-Output 'Media tools and licenses prepared.'
