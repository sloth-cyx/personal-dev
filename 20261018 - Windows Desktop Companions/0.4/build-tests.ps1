param([string]$TestOutputDirectory=(Join-Path $PSScriptRoot 'test-work'))
$ErrorActionPreference='Stop'
New-Item -ItemType Directory -Path $TestOutputDirectory -Force | Out-Null
$TestOutputDirectory=(Resolve-Path -LiteralPath $TestOutputDirectory).Path
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
Push-Location $PSScriptRoot
try {
    foreach($test in @('BehaviorTests','MotionTests','GroundedTests','TurnTests','AnimalTests')) {
        $exe=Join-Path $TestOutputDirectory ($test+'.exe')
        & $compiler /nologo /optimize+ /target:exe "/main:$test" "/out:$exe" /r:System.Windows.Forms.dll /r:System.Drawing.dll /resource:characters.png,characters.png /resource:girl-walk.png,girl-walk.png /resource:girl-standing.png,girl-standing.png /resource:cat-motion.png,cat-motion.png /resource:slime-motion.png,slime-motion.png /resource:DesktopCompanions.ico,AppIcon.ico DesktopCompanions.cs GirlAnimation.cs CompanionBehavior.cs AnimalAnimation.cs ($test+'.cs')
        if($LASTEXITCODE -ne 0){throw "$test 编译失败"}
        Push-Location $TestOutputDirectory
        try { & $exe; if($LASTEXITCODE -ne 0){throw "$test 失败"} } finally {Pop-Location}
    }
} finally {Pop-Location}




