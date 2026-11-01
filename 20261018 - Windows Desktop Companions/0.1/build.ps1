param([string]$OutputPath = (Join-Path $PSScriptRoot 'DesktopCompanions-icon.exe'))
$ErrorActionPreference = 'Stop'
if (Test-Path -LiteralPath $OutputPath) { throw "目标文件已存在，请指定新文件名：$OutputPath" }
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$wpf = Join-Path $framework 'WPF'
$compiler = Join-Path $framework 'csc.exe'
Push-Location $PSScriptRoot
try {
    & $compiler /nologo /target:winexe /utf8output "/out:$OutputPath" /win32icon:DesktopCompanions.ico /resource:DesktopCompanions.ico,AppIcon.ico /r:System.Windows.Forms.dll /r:System.Drawing.dll /resource:characters.png,characters.png DesktopCompanions.cs
    if ($LASTEXITCODE -ne 0) { throw '编译失败' }
} finally { Pop-Location }
