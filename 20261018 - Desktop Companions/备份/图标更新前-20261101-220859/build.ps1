$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    $destination = Join-Path $PSScriptRoot 'DesktopCompanions-rebuilt.exe'
    if (Test-Path -LiteralPath $destination) { throw 'DesktopCompanions-rebuilt.exe 已存在，请先手动重命名后再构建。' }
    & $compiler /nologo /target:winexe /out:DesktopCompanions-rebuilt.exe /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /resource:characters.png,characters.png DesktopCompanions.cs
    if ($LASTEXITCODE -ne 0) { throw '编译失败' }
} finally { Pop-Location }
