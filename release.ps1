# Usage: .\release.ps1 -Version 0.2.0
#
# Bumps PluginInfo.PLUGIN_VERSION, builds locally using the real game DLLs,
# commits, tags, pushes, and creates a GitHub release with the DLL attached.
# Requires: dotnet CLI, gh CLI (authenticated).

param(
    [Parameter(Mandatory)][string]$Version
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$tag      = "v$Version"
$csproj   = "$PSScriptRoot\MegastoreMultiplayer\MegastoreMultiplayer.csproj"
$dll      = "$PSScriptRoot\MegastoreMultiplayer\bin\Release\net472\MegastoreMultiplayer.dll"
$infoFile = "$PSScriptRoot\MegastoreMultiplayer\PluginInfo.cs"

# ── Preflight checks ──────────────────────────────────────────────────────────

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    Write-Error "gh CLI not found. Install from https://cli.github.com"
}

$dirty = git status --porcelain | Where-Object { $_ -notmatch '^\?\?' }
if ($dirty) {
    Write-Error "Uncommitted changes detected. Commit or stash them first.`n$dirty"
}

if (git tag -l $tag) {
    Write-Error "Tag $tag already exists locally. Delete it first with: git tag -d $tag"
}

# ── Bump version in PluginInfo.cs ─────────────────────────────────────────────

Write-Host "Bumping version to $Version..."
$content = Get-Content $infoFile -Raw
$updated = $content -replace 'PLUGIN_VERSION\s*=\s*"[^"]*"', "PLUGIN_VERSION = `"$Version`""
if ($content -eq $updated) {
    Write-Error "Could not find PLUGIN_VERSION in $infoFile"
}
Set-Content $infoFile $updated -NoNewline

# ── Build ─────────────────────────────────────────────────────────────────────

Write-Host "Building $tag..."
dotnet build $csproj -c Release

if (-not (Test-Path $dll)) {
    Write-Error "Build succeeded but DLL not found at: $dll"
}

Write-Host "Built: $dll ($([Math]::Round((Get-Item $dll).Length / 1KB)) KB)"

# ── Commit version bump ───────────────────────────────────────────────────────

git add $infoFile
git commit -m "chore: bump version to $Version"

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
git push origin $tag

# ── Create GitHub release ─────────────────────────────────────────────────────

Write-Host "Creating GitHub release $tag..."
$tempNotes = [System.IO.Path]::GetTempFileName()
$notes | Set-Content $tempNotes
try {
    gh release create $tag $dll --title $tag --notes-file $tempNotes
} finally {
    Remove-Item $tempNotes -ErrorAction SilentlyContinue
}

Write-Host ""
Write-Host "Released: https://github.com/xjacksssss/MegastoreMultiplayer/releases/tag/$tag"
