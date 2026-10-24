param([string]$OutputDirectory = (Join-Path (Split-Path -Parent $PSScriptRoot) 'publish'))
$ErrorActionPreference = 'Stop'
$modernProject = Join-Path $PSScriptRoot 'Groundwork-Joystick-Helper.csproj'
& dotnet publish $modernProject --configuration Release --runtime win-x64 --self-contained true --output $OutputDirectory
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Write-Output "Built version 0.4.0 at $OutputDirectory"
