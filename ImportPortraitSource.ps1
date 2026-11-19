param(
 [Parameter(Mandatory=$true)][string]$Source,
 [Parameter(Mandatory=$true)][string]$SourceId,
 [string]$GameDir = '',
 [string]$TargetModDir = '',
 [switch]$Replace,
 [ValidateSet('en','zh-TW')][string]$Language = 'en'
)
$ErrorActionPreference = 'Stop'
function T([string]$en,[string]$zh) { if($Language -eq 'zh-TW'){$zh}else{$en} }
if ($SourceId -cnotmatch '^[A-Za-z0-9_]+$' -or (($SourceId -ieq 'Redrawn' -or $SourceId -ieq 'RedrawnStudio') -or $SourceId -ieq 'ProviderDefault' -or $SourceId -like 'Native_*')) { throw (T 'SourceId must use only letters, numbers and underscores and cannot be Redrawn, RedrawnStudio, ProviderDefault or begin with Native_.' '來源 ID 只能使用英數字及底線，不可用 Redrawn、RedrawnStudio、ProviderDefault 或 Native_ 開頭。') }
if (!$TargetModDir -and !$GameDir) { throw (T 'Specify -GameDir or -TargetModDir.' '請指定 -GameDir 或 -TargetModDir。') }
if (!$TargetModDir) { $TargetModDir=Join-Path $GameDir 'Mods\RedrawnStudio'; if (!(Test-Path -LiteralPath $TargetModDir)) { $TargetModDir=Join-Path $GameDir 'Mods\RedrawnNative' } }
if (!(Test-Path -LiteralPath (Join-Path $TargetModDir 'manifest.xml') -PathType Leaf)) { throw (T 'TargetModDir must be the installed Redrawn Studio folder.' 'TargetModDir 必須是實際安裝的 Redrawn Studio 資料夾。') }
$sourcePath=(Resolve-Path -LiteralPath $Source).Path
$entries=@()
if (Test-Path -LiteralPath $sourcePath -PathType Container) {
 $map=Join-Path $sourcePath 'portrait-map.csv'
 $folder=Join-Path $sourcePath 'Portraits'
 if (!(Test-Path -LiteralPath $folder -PathType Container)) { throw (T 'Expected a Portraits folder containing PNG files.' '找不到含有 PNG 的 Portraits 資料夾。') }
 if (Test-Path -LiteralPath $map -PathType Leaf) {
  $entries=@(Import-Csv -LiteralPath $map | ForEach-Object { if ($_.key -cmatch '^[A-Za-z0-9_]+$' -and $_.portrait -cmatch '^[A-Za-z0-9_]+$') { [PSCustomObject]@{key=$_.key;file=(Join-Path $folder ($_.portrait+'.png'))} } })
 } else {
  $entries=@(Get-ChildItem -LiteralPath $folder -Filter '*.png' -File | ForEach-Object { [PSCustomObject]@{key=$_.BaseName;file=$_.FullName} })
 }
} else {
 if ([IO.Path]::GetFileName($sourcePath) -ine 'pack.json') { throw (T 'Pass a native portrait-pack folder or a Portrait Studio pack.json file.' '請指定原生立繪包資料夾或 Portrait Studio 的 pack.json。') }
 $json=Get-Content -LiteralPath $sourcePath -Raw -Encoding UTF8 | ConvertFrom-Json
 $entries=@($json.entries | ForEach-Object { if ($_.sprite -and $_.file) { [PSCustomObject]@{key=$_.sprite;file=$_.file} } })
}
$destRoot=Join-Path $TargetModDir 'Sources'; $dest=Join-Path $destRoot $SourceId
if ((Test-Path -LiteralPath $dest) -and !$Replace) { throw (T 'Source already exists. Use -Replace to replace this source only.' '來源已存在；使用 -Replace 僅替換此來源。') }
$stage=Join-Path $TargetModDir ('RedrawnSource_'+[guid]::NewGuid().ToString('N')); $images=Join-Path $stage 'Portraits'
New-Item -ItemType Directory -Path $images -Force | Out-Null
try {
 $seen=@{}; $files=@{}; $rows=New-Object 'System.Collections.Generic.List[object]'; $skipped=0
 foreach($e in $entries) {
  # known scene entries are not counter portrait keys
  if ($e.key -cnotmatch '^[A-Za-z0-9_]+$' -or $e.key -match '^(night_|drjackson|commissary|dumping_ground)' -or !$e.file -or [IO.Path]::GetExtension($e.file) -ine '.png' -or !(Test-Path -LiteralPath $e.file -PathType Leaf) -or $seen.ContainsKey($e.key)) { $skipped++; continue }
  $path=(Resolve-Path -LiteralPath $e.file).Path
  if (!$files.ContainsKey($path)) { $asset='art_'+($files.Count+1).ToString('D4'); Copy-Item -LiteralPath $path -Destination (Join-Path $images ($asset+'.png')); $files[$path]=$asset }
  $seen[$e.key]=$true; $rows.Add([PSCustomObject]@{key=$e.key;portrait=$files[$path]})
 }
 if (!$rows.Count) { throw (T 'No valid portrait entries found. Check paths in pack.json; stale paths cannot be imported.' '找不到有效立繪；請檢查 pack.json 內的路徑，失效路徑無法匯入。') }
 # Write plain CSV, since keys and generated names are restricted to ASCII identifiers.
 $lines=@('key,portrait')+@($rows | ForEach-Object { $_.key+','+$_.portrait })
 [IO.File]::WriteAllLines((Join-Path $stage 'portrait-map.csv'),[string[]]$lines,(New-Object System.Text.UTF8Encoding($false)))
 New-Item -ItemType Directory -Path $destRoot -Force | Out-Null
 $backup=$null
 if (Test-Path -LiteralPath $dest) { $backup=Join-Path $TargetModDir ('SourceBackup_'+$SourceId+'_'+[guid]::NewGuid().ToString('N')); Move-Item -LiteralPath $dest -Destination $backup }
 try { Move-Item -LiteralPath $stage -Destination $dest } catch { if($backup -and !(Test-Path -LiteralPath $dest)){Move-Item -LiteralPath $backup -Destination $dest}; throw }
 Write-Host ((T 'Imported ' '已匯入 ')+$rows.Count+(T ' portrait keys, ' ' 個立繪入口、')+$files.Count+(T ' PNG files; skipped ' ' 張 PNG；略過 ')+$skipped+'.')
 if($backup){Write-Host ((T 'Previous source preserved: ' '舊來源已備份：')+$backup)}
 Write-Host (T 'Open F9 > Source management or refresh PNG / CSV. No source DLLs copied.' '按 F9 開啟來源管理或刷新 PNG / CSV。未複製來源 DLL。')
} finally { if(Test-Path -LiteralPath $stage){Remove-Item -LiteralPath $stage -Recurse -Force} }
