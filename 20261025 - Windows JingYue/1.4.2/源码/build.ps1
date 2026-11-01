param([switch]$Console,[switch]$Preview)
$ErrorActionPreference = 'Stop'
$framework = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319'
$refs = @('System.dll','System.Core.dll','System.Xml.dll','System.Xml.Linq.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','System.Windows.Forms.dll','System.Xaml.dll','WPF\WindowsBase.dll','WPF\PresentationCore.dll','WPF\PresentationFramework.dll') | ForEach-Object { '/reference:' + (Join-Path $framework $_) }
$target = if ($Console) { '/target:exe' } else { '/target:winexe' }
$output = if ($Console) { '/out:' + $PSScriptRoot + '\JingYue.Tests.exe' } elseif ($Preview) { '/out:' + $PSScriptRoot + '\JingYue.Preview.exe' } else { '/out:' + $PSScriptRoot + '\JingYue.exe' }
$definitions = if ($Preview) { '/define:PREVIEW' } else { '/define:RELEASE' }
& (Join-Path $framework 'csc.exe') /nologo $target /optimize+ /platform:anycpu /win32icon:$PSScriptRoot\JingYue.ico $definitions $output @refs $PSScriptRoot\Reader.cs $PSScriptRoot\Formats.cs $PSScriptRoot\Library.cs $PSScriptRoot\AssemblyInfo.cs $PSScriptRoot\IntegrationTests.cs $PSScriptRoot\ReadingImages.cs $PSScriptRoot\UiTheme.cs $PSScriptRoot\DocxHeadings.cs $PSScriptRoot\ReadingLayout.cs $PSScriptRoot\ImageDimensions.cs $PSScriptRoot\ReadingNavigation.cs $PSScriptRoot\AppDialog.cs
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
