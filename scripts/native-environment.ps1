# Shared MSVC environment for target binaries. SDK host tools may still run as x64.
function Get-NativeBuildEnvironment([ValidateSet('x64','arm64')][string]$Architecture) {
 $vswhere='C:/Program Files (x86)/Microsoft Visual Studio/Installer/vswhere.exe'
 $vsRoot=& $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
 if(!$vsRoot){throw 'Visual Studio C++ build tools were not found.'}
 $hostArchitecture='x64'
 if([Runtime.InteropServices.RuntimeInformation]::OSArchitecture -eq [Runtime.InteropServices.Architecture]::Arm64){
  $nativeCompiler=Get-ChildItem -Path "$vsRoot/VC/Tools/MSVC/*/bin/Hostarm64/$Architecture/cl.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
  if($nativeCompiler){$hostArchitecture='arm64'}
 }
 $argument=if($hostArchitecture -eq $Architecture){$Architecture}else{"${hostArchitecture}_$Architecture"}
 [pscustomobject]@{Script=(Join-Path $vsRoot 'VC/Auxiliary/Build/vcvarsall.bat');Argument=$argument}
}
