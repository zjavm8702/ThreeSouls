param(
    [Parameter(Mandatory=$true)][string]$SourceDirectory,
    [Parameter(Mandatory=$true)][ValidatePattern('^[A-Za-z0-9_]+$')][string]$Target,
    [switch]$ValidateOnly
)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$source=(Resolve-Path -LiteralPath $SourceDirectory).Path
$catalogs=@(Get-ChildItem -LiteralPath $source -Filter 'catalog_*.json' -File)
if($catalogs.Count -eq 0){throw 'No JSON catalog in build output'}
$remoteBase="https://zjavm8702.github.io/AddressableAssetsData/$Target/"
foreach($catalog in $catalogs){
    $hash=[IO.Path]::ChangeExtension($catalog.FullName,'.hash')
    if(!(Test-Path -LiteralPath $hash) -or [string]::IsNullOrWhiteSpace([IO.File]::ReadAllText($hash))){throw "Catalog hash missing: $($catalog.Name)"}
    $data=Get-Content -LiteralPath $catalog.FullName -Raw | ConvertFrom-Json
    foreach($id in $data.m_InternalIds){
        if($id.StartsWith($remoteBase) -and $id.EndsWith('.bundle')){
            $name=[Uri]::UnescapeDataString($id.Substring($remoteBase.Length))
            if($name.Contains('/') -or $name.Contains('\') -or !(Test-Path -LiteralPath (Join-Path $source $name))){throw "Remote catalog bundle missing: $name"}
        }
    }
}
$files=@(Get-ChildItem -LiteralPath $source -File | Where-Object {$_.Extension -in '.bundle','.json','.hash'})
if($files.Where({$_.Length -ge 100MB}).Count){throw 'GitHub Pages artifacts must be below the GitHub 100 MiB file limit; reduce bundle size. Do not use LFS for web bundles.'}
if($ValidateOnly){Write-Output "Validated $($files.Count) artifacts for $Target";exit 0}
$cache=Join-Path $projectRoot 'Library/AddressablesPublish'
New-Item -ItemType Directory -Force $cache | Out-Null
$lockPath=Join-Path $cache 'publish.lock'
$lock=$null
try{
    $lock=[IO.File]::Open($lockPath,[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
    $checkout=Join-Path $cache 'Repository'
    function RunGit([string[]]$Arguments){
        & git @Arguments
        if($LASTEXITCODE -ne 0){throw "git failed (exit $LASTEXITCODE): $($Arguments[0])"}
    }
    if(!(Test-Path -LiteralPath (Join-Path $checkout '.git'))){RunGit @('clone','--branch','main','https://github.com/zjavm8702/AddressableAssetsData.git',$checkout)}
    $url=(& git -C $checkout remote get-url origin)
    if($url -ne 'https://github.com/zjavm8702/AddressableAssetsData.git'){throw 'Unexpected publishing remote'}
    $dirty=(& git -C $checkout status --porcelain)
    if($dirty){throw "Publishing checkout contains pending changes: $checkout. Review/retry them before publishing another build."}
    RunGit @('-C',$checkout,'pull','--ff-only','origin','main')
    $destination=Join-Path $checkout $Target
    New-Item -ItemType Directory -Force $destination | Out-Null
    # Copy all artifacts into one commit. Keep old content-addressed bundles for old clients.
    foreach($file in $files){Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $destination $file.Name) -Force}
    RunGit @('-C',$checkout,'add','--',$Target)
    & git -C $checkout diff --cached --quiet
    if($LASTEXITCODE -eq 1){RunGit @('-C',$checkout,'commit','-m',"Publish $Target Addressables $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")}
    elseif($LASTEXITCODE -ne 0){throw 'git diff failed'}
    RunGit @('-C',$checkout,'push','origin','main')
    $commit=(& git -C $checkout rev-parse HEAD)
    [pscustomobject]@{commit=$commit;target=$Target;files=$files.Count;utc=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json | Set-Content (Join-Path $cache 'LastPublish.json') -Encoding utf8
    Write-Output "Published $Target at $commit. GitHub Pages deployment follows the repository Pages configuration."
}finally{if($lock){$lock.Dispose()}}
