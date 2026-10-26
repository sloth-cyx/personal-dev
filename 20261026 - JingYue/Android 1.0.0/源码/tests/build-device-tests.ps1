$ErrorActionPreference='Stop'
$project=Split-Path $PSScriptRoot -Parent
$tools=Join-Path $project '..\android-tools'
$java=(Get-ChildItem (Join-Path $tools 'java') -Directory | Select-Object -First 1).FullName
$env:JAVA_HOME=$java
$bt=Join-Path $tools 'sdk\android-15';$platform=Join-Path $tools 'sdk\android-35\android.jar';$out=Join-Path $project 'build\device-tests'
New-Item -ItemType Directory -Force $out,(Join-Path $out 'classes'),(Join-Path $out 'dex'),(Join-Path $out 'assets') | Out-Null
Copy-Item (Join-Path $PSScriptRoot 'fixtures\fixture.epub') (Join-Path $out 'assets\fixture.epub')
function Check($label){if($LASTEXITCODE -ne 0){throw "$label failed ($LASTEXITCODE)"}}
& (Join-Path $java 'bin\javac.exe') -encoding UTF-8 -source 8 -target 8 -Xlint:-options -bootclasspath ((Join-Path $bt 'core-lambda-stubs.jar')+';'+$platform) -classpath (Join-Path $project 'build\classes.jar') -d (Join-Path $out 'classes') (Join-Path $PSScriptRoot 'DeviceTests.java') (Join-Path $PSScriptRoot 'TestFiles.java'); Check 'Compile tests'
& (Join-Path $java 'bin\jar.exe') cf (Join-Path $out 'classes.jar') -C (Join-Path $out 'classes') .;Check 'Test class archive'
& (Join-Path $bt 'd8.bat') --min-api 26 --lib $platform --classpath (Join-Path $project 'build\classes.jar') --output (Join-Path $out 'dex') (Join-Path $out 'classes.jar'); Check 'Test dex'
& (Join-Path $bt 'aapt2.exe') link -o (Join-Path $out 'unsigned.apk') --manifest (Join-Path $PSScriptRoot 'AndroidTestManifest.xml') -I $platform -A (Join-Path $out 'assets');Check 'Test resources'
Push-Location (Join-Path $out 'dex');try{& (Join-Path $bt 'aapt.exe') add (Join-Path $out 'unsigned.apk') classes.dex;Check 'Test package'}finally{Pop-Location}
& (Join-Path $bt 'zipalign.exe') -f -p 4 (Join-Path $out 'unsigned.apk') (Join-Path $out 'aligned.apk');Check 'Test align'
& (Join-Path $bt 'apksigner.bat') sign --ks (Join-Path $project 'signing\jingyue-release.p12') --ks-pass ('file:'+(Join-Path $project 'signing\keypass.txt')) --out (Join-Path $out 'tests.apk') (Join-Path $out 'aligned.apk');Check 'Sign tests'
