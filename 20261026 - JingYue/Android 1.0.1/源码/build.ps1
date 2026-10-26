param([string]$Tools=(Join-Path $PSScriptRoot '..\android-tools'),[switch]$Test,[string]$Samples='',[string]$Signing=(Join-Path $PSScriptRoot 'signing'))
$ErrorActionPreference='Stop'
$java=(Get-ChildItem (Join-Path $Tools 'java') -Directory | Select-Object -First 1).FullName
$env:JAVA_HOME=$java
$env:Path=(Join-Path $java 'bin')+';'+$env:Path
$bt=Join-Path $Tools 'sdk\android-15'
$platform=Join-Path $Tools 'sdk\android-35\android.jar'
$out=Join-Path $PSScriptRoot 'build'
New-Item -ItemType Directory -Force $out,(Join-Path $out 'classes'),(Join-Path $out 'dex') | Out-Null
function Check($what){if($LASTEXITCODE -ne 0){throw "$what failed ($LASTEXITCODE)"}}
if($Test){
  & (Join-Path $java 'bin\javac.exe') -encoding UTF-8 -d (Join-Path $out 'tests') (Join-Path $PSScriptRoot 'src\com\jingyue\reader\BookParser.java') (Join-Path $PSScriptRoot 'tests\ParserTest.java')
  Check 'Compile parser tests'
  [IO.File]::WriteAllText((Join-Path $out 'samples-path.txt'),$Samples,[Text.UTF8Encoding]::new($false))
  & (Join-Path $java 'bin\java.exe') '-Dfile.encoding=UTF-8' -Xmx512m -cp (Join-Path $out 'tests') com.jingyue.reader.ParserTest ('pathfile:'+(Join-Path $out 'samples-path.txt'))
  Check 'Parser tests'
  return
}
Get-ChildItem (Join-Path $PSScriptRoot 'res') -Recurse -Filter *.xml | ForEach-Object {
  & (Join-Path $bt 'aapt2.exe') compile $_.FullName -o $out
  Check 'Compile resource'
}
$resources=Get-ChildItem $out -Filter *.flat | ForEach-Object FullName
& (Join-Path $bt 'aapt2.exe') link -o (Join-Path $out 'unsigned.apk') --manifest (Join-Path $PSScriptRoot 'AndroidManifest.xml') -I $platform -A (Join-Path $PSScriptRoot 'assets') @resources
Check 'Link resources'
$sources=Get-ChildItem (Join-Path $PSScriptRoot 'src') -Recurse -Filter *.java | ForEach-Object FullName
& (Join-Path $java 'bin\javac.exe') -encoding UTF-8 -source 8 -target 8 -Xlint:-options -bootclasspath ((Join-Path $bt 'core-lambda-stubs.jar')+';'+$platform) -d (Join-Path $out 'classes') @sources
Check 'Compile Java'
& (Join-Path $java 'bin\jar.exe') cf (Join-Path $out 'classes.jar') -C (Join-Path $out 'classes') .
Check 'Class archive'
& (Join-Path $bt 'd8.bat') --release --min-api 26 --lib $platform --output (Join-Path $out 'dex') (Join-Path $out 'classes.jar')
Check 'Dex'
Push-Location (Join-Path $out 'dex')
try { & (Join-Path $bt 'aapt.exe') add (Join-Path $out 'unsigned.apk') classes.dex; Check 'Package dex' } finally {Pop-Location}
& (Join-Path $bt 'zipalign.exe') -f -p 4 (Join-Path $out 'unsigned.apk') (Join-Path $out 'aligned.apk')
Check 'Zip align'
$sign=$Signing
New-Item -ItemType Directory -Force $sign | Out-Null
$pass=Join-Path $sign 'keypass.txt';$key=Join-Path $sign 'jingyue-release.p12'
if(-not(Test-Path $key)){
  if(Test-Path $pass){throw 'Signing password exists without key; inspect signing directory first.'}
  $random=New-Object byte[] 32;[System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($random)
  [IO.File]::WriteAllText($pass,[Convert]::ToBase64String($random))
  & (Join-Path $java 'bin\keytool.exe') -genkeypair -keystore $key -storetype PKCS12 -alias jingyue -keyalg RSA -keysize 3072 -validity 10000 -dname 'CN=JingYue Local Release, O=JingYue, C=MY' '-storepass:file' $pass
  Check 'Create release key'
}
& (Join-Path $bt 'apksigner.bat') sign --ks $key --ks-pass "file:$pass" --out (Join-Path $out 'JingYue-1.0.1.apk') (Join-Path $out 'aligned.apk')
Check 'Sign APK'
& (Join-Path $bt 'apksigner.bat') verify --verbose --print-certs (Join-Path $out 'JingYue-1.0.1.apk')
Check 'Verify APK'
& (Join-Path $bt 'aapt.exe') dump badging (Join-Path $out 'JingYue-1.0.1.apk')
Check 'Manifest verification'
