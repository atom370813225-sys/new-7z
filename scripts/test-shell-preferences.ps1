param([ValidateSet('x64','arm64')][string]$Architecture='x64')
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
. "$PSScriptRoot/native-environment.ps1"
$buildEnvironment=Get-NativeBuildEnvironment $Architecture
$vcvars=$buildEnvironment.Script;$vcArgument=$buildEnvironment.Argument
$output=Join-Path $projectRoot "artifacts/native/$Architecture"
Push-Location $projectRoot
try{
 & cmd /c "`"$vcvars`" $vcArgument >nul && cl /nologo /std:c++17 /EHsc /O2 /MT /utf-8 tests\New7z.ShellPreferences.cpp /Fo`"$output\ShellPreferences.Tests.obj`" /link /OUT:`"$output\ShellPreferences.Tests.exe`" shell32.lib ole32.lib uuid.lib advapi32.lib"
 if($LASTEXITCODE){throw 'Shell preference test build failed.'}
 & "$output/ShellPreferences.Tests.exe" "$output/New7z.Shell.dll" "$projectRoot/artifacts/preferences-fixture" | Tee-Object -FilePath "$projectRoot/docs/shell-preferences-tests.txt"
 if($LASTEXITCODE){throw 'Shell preference tests failed.'}
}finally{Pop-Location}
