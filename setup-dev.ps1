# Run once after cloning: activates the .githooks directory.
git config core.hooksPath .githooks
Write-Host "Git hooks configured. Pre-push hook will refresh ci-libs/ on version tag pushes."
