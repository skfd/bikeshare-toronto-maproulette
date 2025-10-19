#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Run bike share sync for one or all systems

.DESCRIPTION
    PowerShell wrapper script to run the bike share sync tool.
    Makes it easy to sync a single system or all configured systems.

.PARAMETER SystemId
    The ID of the system to sync. If not specified, syncs all systems.

.PARAMETER VerboseOutput
    Show detailed logging output

.PARAMETER QuietOutput
    Show minimal output (errors and summary only)

.PARAMETER DryRun
    Show what would be done without making changes (not yet implemented in tool)

.PARAMETER SkipTests
    Skip running tests before sync

.EXAMPLE
    .\sync.ps1
    Syncs all configured systems

.EXAMPLE
    .\sync.ps1 -SystemId 1
    Syncs only system with ID 1

.EXAMPLE
    .\sync.ps1 -SystemId 1 -VerboseOutput
    Syncs system 1 with detailed logging

.EXAMPLE
    .\sync.ps1 -QuietOutput
    Syncs all systems with minimal output

.NOTES
    This is a local workstation tool for trusted operators.
    Run from the project root directory.
#>

[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [int]$SystemId = 0,

    [switch]$VerboseOutput,
    [switch]$QuietOutput,
    [switch]$DryRun,
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"

# Colors
function Write-Header {
    param([string]$Message)
    Write-Host "`n$Message" -ForegroundColor Cyan
    Write-Host ("=" * $Message.Length) -ForegroundColor DarkCyan
}

function Write-Success {
    param([string]$Message)
    Write-Host "✓ $Message" -ForegroundColor Green
}

function Write-Failure {
    param([string]$Message)
    Write-Host "✗ $Message" -ForegroundColor Red
}

function Write-Info {
    param([string]$Message)
    Write-Host "→ $Message" -ForegroundColor Gray
}

# Check if we're in the right directory
if (-not (Test-Path "src/prepareBikeParking.csproj")) {
    Write-Failure "Must run from project root directory"
    Write-Info "Current directory: $PWD"
    Write-Info "Expected to find: src/prepareBikeParking.csproj"
    exit 1
}

# Run tests unless skipped
if (-not $SkipTests) {
    Write-Header "Running Tests"
    try {
        dotnet test --configuration Release --verbosity quiet --nologo
        Write-Success "All tests passed"
    }
    catch {
        Write-Failure "Tests failed"
        Write-Info "Run with -SkipTests to skip tests, or fix failing tests first"
        exit 1
    }
}

# Build the project
Write-Header "Building Project"
try {
    dotnet build --configuration Release --verbosity quiet --nologo
    Write-Success "Build completed"
}
catch {
    Write-Failure "Build failed"
    exit 1
}

# Load systems from config
function Get-Systems {
    $configPath = "src/bikeshare_systems.json"
    if (-not (Test-Path $configPath)) {
        Write-Failure "Configuration file not found: $configPath"
        exit 1
    }

    try {
        $systems = Get-Content $configPath | ConvertFrom-Json
        return $systems
    }
    catch {
        Write-Failure "Failed to parse $configPath"
        Write-Info $_.Exception.Message
        exit 1
    }
}

# Build command line arguments
function Get-SyncArgs {
    param([int]$Id)

    $cmdArgs = @($Id)

    if ($VerboseOutput) { $cmdArgs += "--verbose" }
    if ($QuietOutput) { $cmdArgs += "--quiet" }

    return $cmdArgs
}

# Run sync for a single system
function Sync-System {
    param(
        [Parameter(Mandatory)]
        [int]$Id,
        [string]$Name
    )

    Write-Header "Syncing System ${Id}: $Name"

    $syncArgs = Get-SyncArgs -Id $Id

    try {
        Push-Location src
        dotnet run --configuration Release --no-build --verbosity quiet -- @syncArgs
        $exitCode = $LASTEXITCODE
        Pop-Location

        if ($exitCode -eq 0) {
            Write-Success "System $Id ($Name) completed successfully"
            return $true
        }
        else {
            Write-Failure "System $Id ($Name) failed with exit code $exitCode"
            return $false
        }
    }
    catch {
        Pop-Location
        Write-Failure "System $Id ($Name) failed with exception"
        Write-Info $_.Exception.Message
        return $false
    }
}

# Main execution
try {
    $systems = Get-Systems
    $totalSystems = $systems.Count

    if ($SystemId -gt 0) {
        # Sync single system
        $system = $systems | Where-Object { $_.id -eq $SystemId }

        if (-not $system) {
            Write-Failure "System with ID $SystemId not found"
            Write-Info "Available system IDs: $($systems.id -join ', ')"
            exit 1
        }

        Write-Header "Bike Share Sync - Single System"
        Write-Info "System: $($system.name) ($($system.city))"
        Write-Info ""

        $success = Sync-System -Id $system.id -Name $system.name

        if ($success) {
            Write-Host "`n" -NoNewline
            Write-Success "Sync completed successfully!"
            exit 0
        }
        else {
            Write-Host "`n" -NoNewline
            Write-Failure "Sync failed"
            exit 1
        }
    }
    else {
        # Sync all systems
        Write-Header "Bike Share Sync - All Systems"
        Write-Info "Processing $totalSystems system(s)"
        Write-Info ""

        $successCount = 0
        $failureCount = 0
        $results = @()

        foreach ($system in $systems | Sort-Object id) {
            $success = Sync-System -Id $system.id -Name $system.name

            $results += [PSCustomObject]@{
                Id      = $system.id
                Name    = $system.name
                Success = $success
            }

            if ($success) {
                $successCount++
            }
            else {
                $failureCount++
            }

            Write-Host ""
        }

        # Summary
        Write-Header "Summary"
        Write-Host ""
        Write-Host "Total systems:    $totalSystems" -ForegroundColor White
        Write-Host "Successful:       $successCount" -ForegroundColor Green

        if ($failureCount -gt 0) {
            Write-Host "Failed:           $failureCount" -ForegroundColor Red
            Write-Host ""
            Write-Host "Failed systems:" -ForegroundColor Yellow
            $results | Where-Object { -not $_.Success } | ForEach-Object {
                Write-Host "  - System $($_.Id): $($_.Name)" -ForegroundColor Red
            }
        }

        Write-Host ""

        if ($failureCount -eq 0) {
            Write-Success "All systems synced successfully!"
            exit 0
        }
        else {
            Write-Failure "Some systems failed to sync"
            exit 1
        }
    }
}
catch {
    Write-Host ""
    Write-Failure "Unexpected error occurred"
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host $_.ScriptStackTrace -ForegroundColor DarkGray
    exit 1
}
finally {
    # Restore location if needed
    if ((Get-Location).Path -ne $PWD) {
        Pop-Location
    }
}
