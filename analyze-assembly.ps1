#Requires -Version 5.1
<#
.SYNOPSIS
    Decompiles Assembly-CSharp.dll and identifies key game classes for Harmony patching.
.PARAMETER GameDir
    Path to the Megastore Simulator installation directory.
.PARAMETER Force
    Re-decompile even if output already exists.
#>
param(
    [string]$GameDir = "C:\Games\Megastore.Simulator.v0.4.1",
    [switch]$Force
)

$ErrorActionPreference = "Stop"
$DllPath = Join-Path $GameDir "Megastore Simulator_Data\Managed\Assembly-CSharp.dll"
$OutDir  = Join-Path $PSScriptRoot "decompiled"
$SrcDir  = Join-Path $OutDir "src"
$CandDir = Join-Path $OutDir "candidates"

# ── Prerequisites ─────────────────────────────────────────────────────────────

if (-not (Get-Command ilspycmd -ErrorAction SilentlyContinue)) {
    Write-Error "ilspycmd not found. Run: dotnet tool install ilspycmd -g"
    exit 1
}

if (-not (Test-Path $DllPath)) {
    Write-Error "DLL not found: $DllPath`nCheck -GameDir parameter."
    exit 1
}

# ── Decompile ─────────────────────────────────────────────────────────────────

$hasSrc = (Test-Path $SrcDir) -and (Get-ChildItem $SrcDir -Filter "*.cs" -Recurse -ErrorAction SilentlyContinue)

if ($Force -or -not $hasSrc) {
    Write-Host "[1/3] Decompiling Assembly-CSharp.dll..." -ForegroundColor Cyan
    if (Test-Path $SrcDir) { Remove-Item $SrcDir -Recurse -Force }
    New-Item -ItemType Directory -Path $SrcDir | Out-Null
    ilspycmd $DllPath -p -o $SrcDir
    Write-Host "      Done." -ForegroundColor Green
} else {
    Write-Host "[1/3] Skipping decompile — src exists. Use -Force to redo." -ForegroundColor DarkGray
}

$allFiles = Get-ChildItem -Path $SrcDir -Filter "*.cs" -Recurse
Write-Host "      $($allFiles.Count) .cs files." -ForegroundColor DarkGray

# ── Candidate search ──────────────────────────────────────────────────────────

Write-Host "[2/3] Searching for candidate classes..." -ForegroundColor Cyan

# Each system has filename keywords (class name patterns) and content keywords
# (method/field names that strongly indicate the right class even if named oddly).
$systems = [ordered]@{
    player        = @{
        File    = @("Player", "FirstPerson", "Character", "LocalPlayer")
        Content = @("Interact\(", "raycast", "heldItem", "PickupItem", "PlayerMove", "PlayerInput")
    }
    shelf_stock   = @{
        File    = @("Shelf", "Stock", "Restock", "ShelfSlot", "ProductSlot", "Display")
        Content = @("AddStock", "RemoveStock", "SetQuantity", "currentStock", "maxStock", "stockLevel")
    }
    economy_money = @{
        File    = @("Economy", "Money", "Balance", "Currency", "Cash", "Finance", "Wallet")
        Content = @("AddMoney", "DeductMoney", "AddFunds", "RemoveFunds", "currentMoney", "playerBalance")
    }
    orders        = @{
        File    = @("Order", "Delivery", "Truck", "Supply", "Wholesale", "Warehouse", "Reorder")
        Content = @("PlaceOrder", "ReceiveDelivery", "CompleteOrder", "orderQueue", "truckArriv")
    }
    inventory     = @{
        File    = @("Inventory", "HeldItem", "ItemSlot", "Carry", "Pickup", "HandSlot")
        Content = @("PickupItem", "DropItem", "AddToInventory", "itemInHand", "carriedItem")
    }
}

if (Test-Path $CandDir) { Remove-Item $CandDir -Recurse -Force }

$report = [System.Text.StringBuilder]::new()
$null = $report.AppendLine("# Assembly-CSharp Candidate Classes")
$null = $report.AppendLine("Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm')")
$null = $report.AppendLine("DLL: $DllPath")
$null = $report.AppendLine("")

foreach ($system in $systems.GetEnumerator()) {
    $systemDir = Join-Path $CandDir $system.Key
    New-Item -ItemType Directory -Path $systemDir | Out-Null

    $matched = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)

    # Pass 1: filename match
    foreach ($kw in $system.Value.File) {
        $allFiles | Where-Object { $_.BaseName -match $kw } | ForEach-Object {
            if ($matched.Add($_.FullName)) {
                Copy-Item $_.FullName (Join-Path $systemDir $_.Name) -Force
            }
        }
    }

    # Pass 2: content match for files not already captured
    $remaining = $allFiles | Where-Object { -not $matched.Contains($_.FullName) }
    foreach ($kw in $system.Value.Content) {
        $remaining | ForEach-Object {
            if (-not $matched.Contains($_.FullName)) {
                if (Select-String -Path $_.FullName -Pattern $kw -Quiet) {
                    if ($matched.Add($_.FullName)) {
                        Copy-Item $_.FullName (Join-Path $systemDir $_.Name) -Force
                    }
                }
            }
        }
    }

    $count = $matched.Count
    $color = if ($count -gt 0) { "Green" } else { "Yellow" }
    Write-Host ("      {0,-15} {1} candidate(s)" -f $system.Key, $count) -ForegroundColor $color

    $null = $report.AppendLine("## $($system.Key)")
    if ($count -eq 0) {
        $null = $report.AppendLine("_No candidates found — class may use an unexpected name._")
    } else {
        foreach ($f in (Get-ChildItem $systemDir | Sort-Object Name)) {
            # Pull the class declaration line so the report shows the real class name
            $decl = Select-String -Path $f.FullName -Pattern "^\s*(public|internal|private).*class\s+\w+" |
                    Select-Object -First 1 -ExpandProperty Line
            $decl = if ($decl) { $decl.Trim() } else { "(class declaration not found)" }
            $null = $report.AppendLine("### ``$($f.BaseName)``")
            $null = $report.AppendLine('```csharp')
            $null = $report.AppendLine($decl)
            $null = $report.AppendLine('```')
            $null = $report.AppendLine("")
        }
    }
    $null = $report.AppendLine("")
}

# ── Report ────────────────────────────────────────────────────────────────────

Write-Host "[3/3] Writing report..." -ForegroundColor Cyan
$reportPath = Join-Path $OutDir "report.md"
$report.ToString() | Set-Content $reportPath -Encoding UTF8
Write-Host "      $reportPath" -ForegroundColor Green
Write-Host ""
Write-Host "Candidates : $CandDir" -ForegroundColor White
Write-Host "Report     : $reportPath" -ForegroundColor White
