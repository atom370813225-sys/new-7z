param([ValidateSet('x64','arm64')][string]$Architecture='x64')
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
. "$PSScriptRoot/native-environment.ps1"
$buildEnvironment=Get-NativeBuildEnvironment $Architecture
$vcvars=$buildEnvironment.Script;$vcArgument=$buildEnvironment.Argument
$output=Join-Path $projectRoot "artifacts/native/$Architecture"
New-Item -ItemType Directory -Path $output -Force | Out-Null
Push-Location $projectRoot
try{
 & "$PSScriptRoot/build-icons.ps1"
 & cmd /c "`"$vcvars`" $vcArgument >nul && rc /nologo /fo `"$output\New7z.Shell.res`" native\New7z.Shell.rc"
 if($LASTEXITCODE){throw 'Shell icon resource build failed.'}
 & cmd /c "`"$vcvars`" $vcArgument >nul && cl /nologo /LD /std:c++17 /EHsc /O2 /MT /utf-8 native\New7z.Shell.cpp /Fo`"$output\New7z.Shell.obj`" /link /DEF:native\New7z.Shell.def /OUT:`"$output\New7z.Shell.dll`" /IMPLIB:`"$output\New7z.Shell.lib`" `"$output\New7z.Shell.res`" shell32.lib shlwapi.lib ole32.lib uuid.lib advapi32.lib"
 if($LASTEXITCODE){throw 'Shell extension build failed.'}
 & cmd /c "`"$vcvars`" $vcArgument >nul && cl /nologo /std:c++17 /EHsc /O2 /MT /utf-8 native\New7z.ShellHost.cpp /Fo`"$output\New7z.ShellHost.obj`" /link /SUBSYSTEM:WINDOWS /OUT:`"$output\New7z.ShellHost.exe`" `"$output\New7z.Shell.res`" shlwapi.lib shell32.lib"
 if($LASTEXITCODE){throw 'Shell launcher build failed.'}
}finally{Pop-Location}

