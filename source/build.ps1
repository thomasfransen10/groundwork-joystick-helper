$ErrorActionPreference = 'Stop'
$helperCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $helperCompiler)) { throw 'The 64-bit .NET Framework compiler was not found.' }
$helperOutput = Join-Path (Split-Path -Parent $PSScriptRoot) 'Groundwork-Joystick-Helper.exe'
& $helperCompiler /nologo /target:winexe /platform:x64 /optimize+ /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Core.dll "/out:$helperOutput" (Join-Path $PSScriptRoot 'Core.cs') (Join-Path $PSScriptRoot 'Controllers.cs') (Join-Path $PSScriptRoot 'App.cs')
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Write-Output $helperOutput
