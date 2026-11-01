param([string]$OutputPath = (Join-Path $PSScriptRoot 'ScreenPen-icon.exe'))
$ErrorActionPreference = 'Stop'
if (Test-Path -LiteralPath $OutputPath) { throw "目标文件已存在，请指定新文件名：$OutputPath" }
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$wpf = Join-Path $framework 'WPF'
$compiler = Join-Path $framework 'csc.exe'
Push-Location $PSScriptRoot
try {
    & $compiler /nologo /target:winexe /utf8output "/out:$OutputPath" /win32icon:ScreenPen.ico /resource:ScreenPen.ico,AppIcon.ico /resource:ScreenPen.png,AppIcon.png /platform:x64 /optimize+ /win32manifest:app.manifest "/r:$wpf\PresentationFramework.dll" "/r:$wpf\PresentationCore.dll" "/r:$wpf\WindowsBase.dll" "/r:$framework\System.Xaml.dll" ScreenPen.cs
    if ($LASTEXITCODE -ne 0) { throw '编译失败' }
} finally { Pop-Location }
