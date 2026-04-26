# Usage: .\release.ps1
#
# Reads the current version from PluginInfo.cs, builds locally using the real
# game DLLs, commits any staged changes, tags, pushes, and creates a GitHub
# release with the DLL attached.
# Requires: dotnet CLI, gh CLI (authenticated).

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$csproj   = "$PSScriptRoot\MegastoreMultiplayer\MegastoreMultiplayer.csproj"
$infoFile = "$PSScriptRoot\MegastoreMultiplayer\PluginInfo.cs"

# ── Read version from PluginInfo.cs ───────────────────────────────────────────

$match = Select-String -Path $infoFile -Pattern 'PLUGIN_VERSION\s*=\s*"([^"]+)"'
if (-not $match) { Write-Error "Could not read PLUGIN_VERSION from $infoFile" }
$Version = $match.Matches[0].Groups[1].Value
$tag     = "v$Version"
Write-Host "Version: $tag (from PluginInfo.cs)"

# ── Preflight checks ──────────────────────────────────────────────────────────

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    Write-Error "gh CLI not found. Install from https://cli.github.com"
}

$dirty = git status --porcelain | Where-Object { $_ -notmatch '^\?\?' }
if ($dirty) {
    Write-Error "Uncommitted changes detected. Commit or stash them first.`n$dirty"
}

if (git tag -l $tag) {
    $existingRelease = gh release view $tag 2>$null
    if ($existingRelease) {
        Write-Host "Release $tag already exists on GitHub — nothing to do."
        exit 0
    }
    # Tag exists locally but no GitHub release — previous attempt was interrupted.
    # Delete the orphaned local tag and continue so this run completes cleanly.
    Write-Host "Orphaned local tag $tag found (no GitHub release). Removing and retrying..."
    git tag -d $tag
}

# ── Build ─────────────────────────────────────────────────────────────────────

# Read GameDir from csproj so we know where CopyToPlugins deployed the DLLs.
$gameDirMatch = Select-String -Path $csproj -Pattern '<GameDir>([^<]+)</GameDir>'
if (-not $gameDirMatch) { Write-Error "Could not read GameDir from $csproj" }
$gameDir   = $gameDirMatch.Matches[0].Groups[1].Value
$pluginDir = "$gameDir\BepInEx\plugins\MegastoreMultiplayer"

Write-Host "Building $tag..."
dotnet build $csproj -c Release

$dlls = [System.IO.Directory]::GetFiles($pluginDir, "*.dll")
if ($dlls.Count -eq 0) { Write-Error "No DLLs found in $pluginDir after build" }
Write-Host "Built $($dlls.Count) DLL(s): $(($dlls | ForEach-Object { [System.IO.Path]::GetFileName($_) }) -join ', ')"

# ── Package into zip ──────────────────────────────────────────────────────────

# Zip layout: BepInEx/plugins/MegastoreMultiplayer/*.dll
# Players extract to game root — no manual folder navigation needed.
$zipPath  = "$PSScriptRoot\MegastoreMultiplayer-$tag.zip"
$tempDir  = Join-Path ([System.IO.Path]::GetTempPath()) "mm_release_$tag"
$tempDest = "$tempDir\BepInEx\plugins\MegastoreMultiplayer"

if (Test-Path $zipPath) { Remove-Item $zipPath }
if (Test-Path $tempDir) { Remove-Item $tempDir -Recurse -Force }
New-Item -ItemType Directory -Force $tempDest | Out-Null

foreach ($dll in $dlls) { Copy-Item $dll $tempDest }

Compress-Archive -Path "$tempDir\*" -DestinationPath $zipPath
Remove-Item $tempDir -Recurse -Force

Write-Host "Packaged: $zipPath ($([Math]::Round((Get-Item $zipPath).Length / 1KB)) KB)"

# ── Commit version bump ───────────────────────────────────────────────────────

git add $infoFile
git diff --cached --quiet || git commit -m "chore: release $tag"

# ── Generate changelog ────────────────────────────────────────────────────────

$prevTag = git describe --tags --abbrev=0 HEAD^ 2>$null
$notes   = if ($prevTag) {
    git log "${prevTag}..HEAD" --pretty=format:"- %s" --no-merges
} else {
    git log --pretty=format:"- %s" --no-merges
}

# ── Tag & push ────────────────────────────────────────────────────────────────

Write-Host "Tagging $tag and pushing..."
git tag $tag
git push
# Only push the tag if it doesn't already exist on the remote.
$remoteTag = git ls-remote --tags origin $tag 2>$null
if ($remoteTag) {
    Write-Host "Tag $tag already on remote — skipping tag push."
} else {
    git push origin $tag
}

# ── Create GitHub release ─────────────────────────────────────────────────────

Write-Host "Creating GitHub release $tag..."
$tempNotes = [System.IO.Path]::GetTempFileName()
$notes | Set-Content $tempNotes
try {
    gh release create $tag $zipPath --title $tag --notes-file $tempNotes
} finally {
    Remove-Item $tempNotes  -ErrorAction SilentlyContinue
    Remove-Item $zipPath    -ErrorAction SilentlyContinue
}

Write-Host ""
Write-Host "Released: https://github.com/xjacksssss/MegastoreMultiplayer/releases/tag/$tag"
