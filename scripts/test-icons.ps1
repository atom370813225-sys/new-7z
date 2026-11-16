param([string]$IdentityDirectory)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
Add-Type -TypeDefinition @'
using System.Runtime.InteropServices;
public static class New7zIconResources {
 [DllImport("shell32.dll", CharSet=CharSet.Unicode)]
 public static extern uint ExtractIconEx(string file,int index,System.IntPtr large,System.IntPtr small,uint count);
}
'@
$checks=@(foreach($relative in @('New7z.App.exe','New7z.Shell.dll','New7z.ShellHost.exe')){
 $file=Join-Path $projectRoot "artifacts/app/$relative"
 $count=[New7zIconResources]::ExtractIconEx($file,-1,[IntPtr]::Zero,[IntPtr]::Zero,0)
 if($count -lt 1){throw "No Windows icon resources found: $relative"}
 [pscustomobject]@{File=$relative;IconGroups=$count}
})
$ico=Join-Path $projectRoot 'artifacts/app/Assets/New7z.ico'
$reader=[IO.BinaryReader]::new([IO.File]::OpenRead($ico))
try{
 if($reader.ReadUInt16() -ne 0 -or $reader.ReadUInt16() -ne 1){throw 'Invalid ICO header.'}
 $count=$reader.ReadUInt16();if($count -ne 7){throw 'Expected seven ICO sizes.'}
 $sizes=@(for($i=0;$i -lt $count;$i++){
  $size=$reader.ReadByte();$reader.ReadBytes(15) | Out-Null
  if($size -eq 0){256}else{[int]$size}
 })
}finally{$reader.Dispose()}
Add-Type -AssemblyName System.Drawing
$unplated=0
foreach($size in @(16,20,24,30,32,36,40,44,48,60,64,72,80,96,256)){
 foreach($variant in @('unplated','lightunplated')){
  $file=Join-Path $projectRoot "src/New7z.App/Assets/Store/Square44x44Logo.targetsize-$($size)_altform-$variant.png"
  $bitmap=[Drawing.Bitmap]::FromFile($file)
  try{if($bitmap.Width -ne $size -or $bitmap.Height -ne $size -or $bitmap.GetPixel(0,0).A -ne 0){throw "Invalid unplated taskbar image: $file"}}finally{$bitmap.Dispose()}
  $unplated++
 }
}
if($IdentityDirectory){
 $IdentityDirectory=[IO.Path]::GetFullPath($IdentityDirectory)
 $sdk=Get-ChildItem 'C:/Program Files (x86)/Windows Kits/10/bin' -Directory | Where-Object {$_.Name -match '^10\.' -and (Test-Path -LiteralPath "$($_.FullName)/x64/makepri.exe")} | Sort-Object {[version]$_.Name} -Descending | Select-Object -First 1
 $dump=Join-Path $projectRoot 'artifacts/identity-icon-pri.xml'
 & "$($sdk.FullName)/x64/makepri.exe" dump /if (Join-Path $IdentityDirectory 'resources.pri') /of $dump /dt detailed /o | Out-Null
 if($LASTEXITCODE){throw 'Cannot inspect identity resource index.'}
 $resourceText=Get-Content -LiteralPath $dump -Raw
 foreach($qualifier in @('UNPLATED','LIGHTUNPLATED')){if($resourceText.IndexOf(('name="AlternateForm" value="'+$qualifier+'"'),[StringComparison]::OrdinalIgnoreCase) -lt 0){throw "Identity PRI lacks the $qualifier qualifier."}}
}
[pscustomobject]@{Result='PASS';Resources=$checks;IconSizes=$sizes;UnplatedAssets=$unplated;IdentityPri=$IdentityDirectory} | ConvertTo-Json -Depth 4 | Tee-Object -FilePath "$projectRoot/docs/icon-integration.json"
