param([Parameter(Mandatory)][string]$PayloadDirectory,[ValidateSet('x64','arm64')][string]$Architecture='x64')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Reflection.Metadata
$PayloadDirectory=[IO.Path]::GetFullPath($PayloadDirectory)
$expected=if($Architecture -eq 'arm64'){'Arm64'}else{'Amd64'}
$native=0;$neutral=0
foreach($name in @('New7z.App.exe','New7z.EngineHost.exe','7z.dll','New7z.Native.dll','New7z.Shell.dll','New7z.ShellHost.exe')){
 if(!(Test-Path -LiteralPath (Join-Path $PayloadDirectory $name))){throw "Missing target binary: $name"}
}
foreach($file in Get-ChildItem -LiteralPath $PayloadDirectory -Recurse -File | Where-Object {$_.Extension -in @('.dll','.exe')}){
 $stream=[IO.File]::OpenRead($file.FullName)
 $reader=[Reflection.PortableExecutable.PEReader]::new($stream)
 try{
  $headers=$reader.PEHeaders;$machine=$headers.CoffHeader.Machine.ToString();$cor=$headers.CorHeader
  $anyCpu=$machine -eq 'I386' -and $null -ne $cor -and ([int]$cor.Flags -band 1) -ne 0 -and ([int]$cor.Flags -band 2) -eq 0 -and $cor.ManagedNativeHeaderDirectory.Size -eq 0
  if($anyCpu){$neutral++}
  elseif($machine -eq $expected){$native++}
  else{throw "Architecture mismatch: $($file.FullName) is $machine; expected $expected or AnyCPU."}
 }finally{$reader.Dispose();$stream.Dispose()}
}
foreach($name in @('New7z.App','New7z.EngineHost')){
 $deps=Get-Content -LiteralPath (Join-Path $PayloadDirectory "$name.deps.json") -Raw | ConvertFrom-Json
 if($deps.runtimeTarget.name -notlike "*/win-$Architecture"){throw "Wrong runtime target in $name.deps.json"}
}
$manifest=Join-Path $PayloadDirectory 'ShellIdentity/AppxManifest.xml'
if(Test-Path -LiteralPath $manifest){
 if(([xml](Get-Content -LiteralPath $manifest -Raw)).Package.Identity.ProcessorArchitecture -ne $Architecture){throw 'Shell identity architecture mismatch.'}
}
[pscustomobject]@{Result='PASS';Architecture=$Architecture;TargetBinaries=$native;AnyCpuAssemblies=$neutral} | ConvertTo-Json
