param([switch]$SkipEngine,[ValidateSet('x64','arm64')][string]$Architecture='x64')
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
. "$PSScriptRoot/native-environment.ps1"
$buildEnvironment=Get-NativeBuildEnvironment $Architecture
$vcvars=$buildEnvironment.Script;$vcArgument=$buildEnvironment.Argument
$source=Join-Path $projectRoot '7zSdk/Source'
if(!(Test-Path -LiteralPath "$source/CPP/7zip/Bundles/Format7zF/makefile")){
  & "$projectRoot/7zSdk/bin/x64/7zr.exe" x "$projectRoot/7zSdk/7z2605-src.7z" "-o$source" -y
  if($LASTEXITCODE){throw 'Source extraction failed.'}
}
$output=Join-Path $projectRoot "artifacts/native/$Architecture"
New-Item -ItemType Directory -Path $output -Force | Out-Null
if(!$SkipEngine){
  Push-Location "$source/CPP/7zip/Bundles/Format7zF"
  try { & cmd /c "`"$vcvars`" $vcArgument >nul && nmake /nologo /f makefile PLATFORM=$Architecture"; if($LASTEXITCODE){throw '7z.dll build failed.'} } finally { Pop-Location }
}
Copy-Item -LiteralPath "$source/CPP/7zip/Bundles/Format7zF/$Architecture/7z.dll" -Destination $output -Force
Push-Location $projectRoot
try { & cmd /c "`"$vcvars`" $vcArgument >nul && cl /nologo /LD /std:c++17 /EHsc /O2 /MT /utf-8 /DUNICODE /D_UNICODE native\New7z.Native.cpp /Fo`"$output\New7z.Native.obj`" /link /OUT:`"$output\New7z.Native.dll`" /IMPLIB:`"$output\New7z.Native.lib`" ole32.lib oleaut32.lib user32.lib shell32.lib urlmon.lib";if($LASTEXITCODE){throw 'Native bridge build failed.'} } finally { Pop-Location }
Write-Host "Native engine: $output"
