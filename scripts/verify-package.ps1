<#
.SYNOPSIS
Checks a submission ZIP from a clean extracted copy.

.DESCRIPTION
Extracts the ZIP into a new folder outside the repository, then follows the extracted README:
builds the solution and runs the ordinary tests (no external services), installs, lints, and
builds the frontend from its lockfile, and runs scripts/verify-compose.sh, which starts Docker
Compose with a fresh .env and new volumes and checks database initialization, the gateway, login,
an access check, ticket creation and resolution, and the resolution email. On Windows, the Compose check runs in WSL when `docker` is not available in
PowerShell. Exits with 1 if a check fails.

.EXAMPLE
pwsh scripts/verify-package.ps1 -ZipPath submission/University_Helpdesk-1234567.zip
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$ZipPath,
    # Must not exist yet. Defaults to a new folder in the temporary directory.
    [string]$Destination,
    [switch]$SkipCompose,
    # Start Compose with the existing university-helpdesk-*:local images instead of building them.
    [switch]$UseCachedImages,
    # Build the API, worker, and gateway images with --no-cache --pull, so no earlier layer is reused.
    [switch]$FreshImages,
    [switch]$KeepRunning
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem

$ZipPath = (Resolve-Path $ZipPath).Path
if (-not $Destination) {
    $Destination = Join-Path ([IO.Path]::GetTempPath()) "helpdesk-clean-$(Get-Date -Format yyyyMMdd-HHmmss)"
}
if (Test-Path $Destination) { throw "$Destination already exists; use a new folder." }
$Destination = [IO.Path]::GetFullPath($Destination)

$repo = (git -C $PSScriptRoot rev-parse --show-toplevel 2> $null)
if ($repo -and $Destination.StartsWith([IO.Path]::GetFullPath($repo), [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Extract outside the repository, so that no local file can be picked up.'
}

$results = [ordered]@{}
function Step([string]$name, [scriptblock]$action) {
    Write-Host "== $name" -ForegroundColor Cyan
    $watch = [Diagnostics.Stopwatch]::StartNew()
    & $action
    $ok = $LASTEXITCODE -eq 0
    $results[$name] = '{0} ({1:N0} s)' -f ($(if ($ok) { 'passed' } else { 'FAILED' })), $watch.Elapsed.TotalSeconds
    if (-not $ok) { throw "$name failed." }
}

try {
    Step 'Extract' {
        [IO.Compression.ZipFile]::ExtractToDirectory($ZipPath, $Destination)
        $global:LASTEXITCODE = 0
    }
    $root = Get-ChildItem $Destination -Directory | Select-Object -First 1 -ExpandProperty FullName
    Write-Host "Extracted to $root"
    Push-Location $root
    $pushed = $true

    # The ordinary suite must not pick up service settings from this shell.
    Get-ChildItem Env: | Where-Object Name -like 'HELPDESK_TEST_*' | ForEach-Object { Remove-Item "Env:$($_.Name)" }

    Step 'Build (dotnet build)' { dotnet build UniversityHelpdesk.slnx -c Release -nologo -v q }
    Step 'Ordinary tests (dotnet test)' { dotnet test UniversityHelpdesk.slnx -c Release --no-build -nologo -v q }
    if (Get-Command npm -ErrorAction SilentlyContinue) {
        Push-Location frontend
        try {
            Step 'Frontend (npm ci, lint, build)' {
                npm ci --no-audit --no-fund
                if ($LASTEXITCODE -eq 0) { npm run lint }
                if ($LASTEXITCODE -eq 0) { npm run build }
            }
        }
        finally { Pop-Location }
    }
    else {
        $results['Frontend (npm ci, lint, build)'] = 'skipped: npm not found (the gateway image build below still runs them)'
    }

    if (-not $SkipCompose) {
        $env:USE_CACHED_IMAGES = if ($UseCachedImages) { '1' } else { '' }
        $env:FRESH_IMAGES = if ($FreshImages) { '1' } else { '' }
        $env:KEEP_RUNNING = if ($KeepRunning) { '1' } else { '' }
        Step 'Docker Compose (scripts/verify-compose.sh)' {
            if (Get-Command docker -ErrorAction SilentlyContinue) {
                bash scripts/verify-compose.sh
            }
            else {
                $linuxPath = (wsl -e wslpath -a ($root -replace '\\', '/')).Trim()
                wsl -e env "USE_CACHED_IMAGES=$env:USE_CACHED_IMAGES" "FRESH_IMAGES=$env:FRESH_IMAGES" "KEEP_RUNNING=$env:KEEP_RUNNING" bash "$linuxPath/scripts/verify-compose.sh"
            }
        }
    }
}
finally {
    if ($pushed) { Pop-Location }
    Write-Host ''
    Write-Host "Clean-copy results for $(Split-Path $ZipPath -Leaf):"
    $results.GetEnumerator() | ForEach-Object { Write-Host ("  {0}: {1}" -f $_.Key, $_.Value) }
    if ($SkipCompose) { Write-Host '  Docker Compose: skipped (-SkipCompose)' }
    Write-Host "  Extracted copy: $Destination"
}
