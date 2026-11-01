$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$wpf = Join-Path $framework 'WPF'
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /utf8output "/win32manifest:$PSScriptRoot\app.manifest" "/out:$PSScriptRoot\ScreenPen.exe" "/r:$wpf\PresentationFramework.dll" "/r:$wpf\PresentationCore.dll" "/r:$wpf\WindowsBase.dll" "/r:$framework\System.Xaml.dll" "$PSScriptRoot\ScreenPen.cs"
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
