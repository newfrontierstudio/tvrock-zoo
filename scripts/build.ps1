param([switch]$Installer,[string]$OutputRoot='',[string]$QuantumWorkerPath='')
$ErrorActionPreference='Stop'
& (Join-Path $PSScriptRoot 'build-shared.ps1') -Installer:$Installer -OutputRoot $OutputRoot -QuantumWorkerPath $QuantumWorkerPath
