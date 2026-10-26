param([string]$Tools=(Join-Path $PSScriptRoot '..\android-tools'))
$ErrorActionPreference='Stop'
New-Item -ItemType Directory -Force $Tools | Out-Null
$archives=@(
 @{File='java.zip';Url='https://cdn.azul.com/zulu/bin/zulu17.70.111-ca-jdk17.0.21-win_x64.zip';Hash='ED209FF4293FF581C81D3B4268FAF108FBE610E6ED43883DE9E5F04308D68A72';Folder='java'},
 @{File='build-tools.zip';Url='https://dl.google.com/android/repository/build-tools_r35_windows.zip';Hash='5753C679A1B90BCF6FBC9945A2CE39DFB9E74F1DF0831A1E1866AE5B594326F0';Folder='sdk'},
 @{File='platform.zip';Url='https://dl.google.com/android/repository/platform-35_r02.zip';Hash='0988CACAD01B38A18A47BAC14A0695F246BC76C1B06C0EEB8EB0DC825AB0C8E0';Folder='sdk'}
)
foreach($a in $archives){
  $file=Join-Path $Tools $a.File
  if(-not(Test-Path -LiteralPath $file)){Invoke-WebRequest $a.Url -OutFile $file}
  if((Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -ne $a.Hash){throw ('Archive checksum mismatch: '+$a.File)}
  Expand-Archive -LiteralPath $file -DestinationPath (Join-Path $Tools $a.Folder) -Force
}
Write-Output 'Build tools prepared. Run .\build.ps1 to compile and sign the APK.'
