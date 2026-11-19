param(
 [string]$GameDir = '',
 [string]$InstalledModDir = '',
 [switch]$Install,
 [ValidateSet('en','zh-TW')][string]$Language = 'en'
)
$ErrorActionPreference = 'Stop'
function T([string]$en,[string]$zh) { if($Language -eq 'zh-TW'){$zh}else{$en} }
if (!$GameDir) { throw (T 'Specify -GameDir for your local game installation.' '請使用 -GameDir 指定本機遊戲安裝目錄。') }
$root=$PSScriptRoot
$old=Join-Path $root 'RedrawnNative'; $new=Join-Path $root 'RedrawnStudio'
$release=Join-Path $root 'ReleaseData'
if (!(Test-Path -LiteralPath (Join-Path $GameDir 'Probably Stolen_Data\Managed\Assembly-CSharp.dll'))) { throw (T 'Game assemblies not found. Check -GameDir.' '找不到遊戲程式集，請確認 -GameDir。') }
if (!(Get-Command dotnet -ErrorAction SilentlyContinue)) { throw (T 'Install .NET SDK 6 or newer.' '請先安裝 .NET SDK 6 或更新版本。') }
if ((Test-Path -LiteralPath $old) -and (Test-Path -LiteralPath $new)) { throw (T 'Both legacy and new project asset folders exist. Back up and merge them manually first.' '新舊工程素材資料夾同時存在。請先備份並手動合併。') }
if (!(Test-Path -LiteralPath $old) -and !(Test-Path -LiteralPath $new)) { throw (T 'No asset folder found. Extract the full source package or apply this patch to the old project.' '找不到素材資料夾；請使用完整工程，或將補丁解壓至舊工程。') }
& dotnet build (Join-Path $root 'RedrawnStudio.csproj') -c Release "-p:GameDir=$GameDir"
if ($LASTEXITCODE -ne 0) { throw (T 'Build failed; no assets or installed mod were changed.' '編譯失敗，未修改素材或已安裝模組。') }
$dll=Join-Path $root 'bin\Release\netstandard2.1\RedrawnStudio.dll'
if (!(Test-Path -LiteralPath $dll -PathType Leaf)) { throw (T 'Expected DLL not found.' '找不到編譯完成的 DLL。') }
# Rename only after a successful build. Existing assets and user configuration stay in place.
if (Test-Path -LiteralPath $old) { Move-Item -LiteralPath $old -Destination $new }
function UpdateRuntime([string]$dest) {
 Copy-Item -LiteralPath $dll -Destination (Join-Path $dest 'RedrawnStudio.dll') -Force
 Copy-Item -LiteralPath (Join-Path $release 'studio-catalog.tsv') -Destination (Join-Path $dest 'studio-catalog.tsv') -Force
 Copy-Item -LiteralPath (Join-Path $release 'manifest.xml') -Destination (Join-Path $dest 'manifest.xml') -Force
 $legacy=Join-Path $dest 'RedrawnNative.dll'; if(Test-Path -LiteralPath $legacy -PathType Leaf){Remove-Item -LiteralPath $legacy -Force}
}
UpdateRuntime $new
$legacyProject=Join-Path $root 'RedrawnNative.csproj'; if(Test-Path -LiteralPath $legacyProject -PathType Leaf){Remove-Item -LiteralPath $legacyProject -Force}
Write-Host (T 'Built Redrawn Studio 1.0.0.' 'Redrawn Studio 1.0.0 編譯完成。')
if (!$Install) { Write-Host (T 'Close the game, then run again with -Install to upgrade the local mod or copy the built RedrawnStudio folder for a fresh install.' '請關閉遊戲，再加上 -Install 更新本機模組；全新安裝可直接複製編譯後的 RedrawnStudio 資料夾。'); return }
# User must close game; never install into Workshop or overwrite another mod ID.
if (Get-Process -Name 'Probably Stolen' -ErrorAction SilentlyContinue) { throw (T 'Close Probably Stolen before installing.' '請先關閉遊戲再安裝。') }
$mods=Join-Path $GameDir 'Mods'; $dest=Join-Path $mods 'RedrawnStudio'; $legacyDest=Join-Path $mods 'RedrawnNative'
if ($InstalledModDir) { $existing=(Resolve-Path -LiteralPath $InstalledModDir).Path; $dest=Join-Path (Split-Path -Parent $existing) 'RedrawnStudio' }
else {
 if (!(Test-Path -LiteralPath $mods)){New-Item -ItemType Directory -Path $mods -Force | Out-Null}
 $matches=@(Get-ChildItem -LiteralPath $mods -Directory | Where-Object { $m=Join-Path $_.FullName 'manifest.xml'; if(Test-Path -LiteralPath $m -PathType Leaf){try{([xml](Get-Content -LiteralPath $m -Raw)).ModManifest.ID -eq 'evansgoethe.redrawn'}catch{$false}}else{$false} })
 if($matches.Count -gt 1){throw (T 'Multiple installed copies found. Keep only one before installing.' '找到多個已安裝副本，請先保留唯一一份。')}
 $existing=if($matches.Count -eq 1){$matches[0].FullName}else{''}
}
if($existing){
 $manifest=Join-Path $existing 'manifest.xml'; if(!(Test-Path -LiteralPath $manifest) -or ([xml](Get-Content -LiteralPath $manifest -Raw)).ModManifest.ID -ne 'evansgoethe.redrawn'){throw (T 'The selected installed folder is not Redrawn Studio.' '選定資料夾不是 Redrawn Studio。')}
 if($existing -ne $dest -and (Test-Path -LiteralPath $dest)){throw (T 'Destination already exists.' '目標資料夾已存在。')}
 # Backup outside the Mods folder, avoiding duplicate mod IDs.
 $backupRoot=Join-Path (Split-Path -Parent (Split-Path -Parent $existing)) 'RedrawnStudio_Backups'
 New-Item -ItemType Directory -Path $backupRoot -Force | Out-Null
 $backup=Join-Path $backupRoot ((Get-Date -Format 'yyyyMMdd_HHmmss')+'_'+[guid]::NewGuid().ToString('N').Substring(0,8))
 Copy-Item -LiteralPath $existing -Destination $backup -Recurse
 if($existing -ne $dest){Move-Item -LiteralPath $existing -Destination $dest}
 UpdateRuntime $dest
 Write-Host ((T 'Previous installation backed up: ' '舊版已備份：')+$backup)
} else {
 if(Test-Path -LiteralPath $dest){throw (T 'Destination exists but was not recognized as Redrawn Studio.' '目標資料夾存在，但無法確認為 Redrawn Studio。')}
 Copy-Item -LiteralPath $new -Destination $dest -Recurse
}
Write-Host ((T 'Installed: ' '已安裝：')+$dest)
