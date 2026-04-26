# Usage: .\release.ps1
#
# Reads the current version from PluginInfo.cs, builds locally using the real
# game DLLs, commits any staged changes, tags, pushes, and creates a GitHub
# release with the DLL attached.
# Requires: dotnet CLI, gh CLI (authenticated).

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$csproj   = "$PSScriptRoot\MegastoreMultiplayer\MegastoreMultiplayer.csproj"
$dll      = "$PSScriptRoot\MegastoreMultiplayer\bin\Release\net472\MegastoreMultiplayer.dll"
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
    # If the release already exists on GitHub this is a no-op re-run — bail cleanly.
    $existingRelease = gh release view $tag 2>$null
    if ($existingRelease) {
        Write-Host "Release $tag already exists on GitHub — nothing to do."
        exit 0
    }
    Write-Error "Tag $tag exists locally but has no GitHub release. Delete the tag and retry: git tag -d $tag"
}

# ── Build ─────────────────────────────────────────────────────────────────────

Write-Host "Building $tag..."
dotnet build $csproj -c Release

if (-not (Test-Path $dll)) {
    Write-Error "Build succeeded but DLL not found at: $dll"
}

Write-Host "Built: $dll ($([Math]::Round((Get-Item $dll).Length / 1KB)) KB)"

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
