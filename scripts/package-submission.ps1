<#
.SYNOPSIS
Builds the submission ZIP from committed files and checks what it contains.

.DESCRIPTION
The archive is created with `git archive`, so it holds only files committed at -Ref: no
.git folder, no ignored files (.env, secrets.env, bin/, obj/, test results), and no
uncommitted changes. The script then adds database/schema.sql, generated from the EF Core
migrations of that commit, and optionally docs/presentation.pdf, exported with Marp.

Finally it checks that every required item is present, that no excluded file is present,
and that no text file contains a private key, a real token, or a value from the local .env
or secrets.env files. Values are never printed. The script exits with 1 if a check fails.

.EXAMPLE
powershell -ExecutionPolicy Bypass -File scripts/package-submission.ps1 -IncludeSlidesPdf

.EXAMPLE
powershell -ExecutionPolicy Bypass -File scripts/package-submission.ps1 -CheckZip Downloads/University_Helpdesk-1234567.zip
#>
[CmdletBinding()]
param(
    # Commit (or tree) to package.
    [string]$Ref = 'HEAD',
    # Folder for the ZIP; the default, submission/, is ignored by Git.
    [string]$OutputDirectory,
    # Also export docs/presentation.md to docs/presentation.pdf (needs Node.js and Chrome or Edge).
    [switch]$IncludeSlidesPdf,
    # Only check an existing ZIP, for example the copy downloaded back from Moodle.
    [string]$CheckZip
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem

$repo = (git -C $PSScriptRoot rev-parse --show-toplevel).Trim()
$prefix = 'University_Helpdesk'
$generated = @{}

if ($CheckZip) {
    $zipPath = (Resolve-Path $CheckZip).Path
    $short = '(existing ZIP)'
}
else {
    if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repo 'submission' }
    New-Item -ItemType Directory -Force $OutputDirectory | Out-Null

    $short = (git -C $repo rev-parse --short=7 $Ref).Trim()
    if ($LASTEXITCODE -ne 0) { throw "Unknown ref '$Ref'." }
    $zipPath = Join-Path (Resolve-Path $OutputDirectory) "$prefix-$short.zip"

    if ($Ref -eq 'HEAD' -and (git -C $repo status --porcelain)) {
        Write-Warning 'The working tree has uncommitted or untracked changes. They are not included in the package.'
    }

    # 1. Committed files only.
    if (Test-Path $zipPath) { Remove-Item $zipPath }
    git -C $repo archive --format=zip --prefix="$prefix/" --output="$zipPath" $Ref
    if ($LASTEXITCODE -ne 0) { throw 'git archive failed.' }

    # 2. Generated files, built from the archived copy rather than the working tree.
    $staging = Join-Path ([IO.Path]::GetTempPath()) "helpdesk-package-$([guid]::NewGuid().ToString('N'))"
    try {
        [IO.Compression.ZipFile]::ExtractToDirectory($zipPath, $staging)
        $source = Join-Path $staging $prefix

        Push-Location $source
        try {
            dotnet tool restore | Out-Null
            if ($LASTEXITCODE -ne 0) { throw 'dotnet tool restore failed.' }
            dotnet build backend/UniversityHelpdesk.Api -nologo -v q | Out-Null
            if ($LASTEXITCODE -ne 0) { throw 'Building the API for the schema script failed.' }
            $schema = Join-Path $staging 'schema.sql'
            dotnet ef migrations script --idempotent --no-build --project backend/UniversityHelpdesk.Api --output $schema | Out-Null
            if ($LASTEXITCODE -ne 0) { throw 'Generating database/schema.sql failed.' }
            $generated['database/schema.sql'] = $schema

            if ($IncludeSlidesPdf) {
                $pdf = Join-Path $staging 'presentation.pdf'
                # Marp logs progress to stderr, which Windows PowerShell 5.1 would treat as an error.
                $ErrorActionPreference = 'Continue'
                # --allow-local-files lets the slides embed the screenshots in docs/screenshots/.
                npx --yes @marp-team/marp-cli@4 docs/presentation.md --pdf --allow-local-files --output $pdf 2>&1 | Out-Null
                $ErrorActionPreference = 'Stop'
                if ($LASTEXITCODE -ne 0 -or -not (Test-Path $pdf)) { throw 'Exporting docs/presentation.pdf failed.' }
                $generated['docs/presentation.pdf'] = $pdf
            }
        }
        finally {
            Pop-Location
        }

        $zip = [IO.Compression.ZipFile]::Open($zipPath, 'Update')
        try {
            foreach ($entry in $generated.GetEnumerator()) {
                [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $entry.Value, "$prefix/$($entry.Key)") | Out-Null
            }
        }
        finally {
            $zip.Dispose()
        }
    }
    finally {
        if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
    }
}

# 3. Check the contents.
$failures = [Collections.Generic.List[string]]::new()
$zip = [IO.Compression.ZipFile]::OpenRead($zipPath)
try {
    $files = @($zip.Entries | Where-Object { $_.Name } | ForEach-Object { $_.FullName.Substring($prefix.Length + 1) })

    function Read-Entry([string]$path) {
        $reader = [IO.StreamReader]::new($zip.GetEntry("$prefix/$path").Open())
        try { $reader.ReadToEnd() } finally { $reader.Dispose() }
    }

    $required = [ordered]@{
        'Source code'                    = 'UniversityHelpdesk.slnx', 'backend/UniversityHelpdesk.Api/Program.cs',
                                           'backend/UniversityHelpdesk.NotificationWorker/Program.cs',
                                           'benchmarks/UniversityHelpdesk.Benchmark/Program.cs'
        'Frontend source'                = 'frontend/package.json', 'frontend/package-lock.json', 'frontend/index.html',
                                           'frontend/src/main.tsx', 'frontend/vite.config.ts', 'frontend/Dockerfile'
        'Configuration and run scripts'  = '.env.example', 'compose.yaml', 'deploy/gateway.conf',
                                           'deploy/kubernetes/infrastructure/secrets.env.example',
                                           'backend/UniversityHelpdesk.Api/appsettings.json',
                                           'backend/UniversityHelpdesk.Api/Dockerfile',
                                           'backend/UniversityHelpdesk.NotificationWorker/Dockerfile',
                                           '.github/workflows/ci.yml', 'scripts/package-submission.ps1',
                                           'scripts/verify-package.ps1', 'scripts/verify-compose.sh'
        'Database schema and init'       = 'database/schema.sql', 'database/init/01-create-test-database.sql'
        'Tests and evidence'             = 'tests/postman/UniversityHelpdesk.postman_collection.json',
                                           'tests/postman/helpdesk.postman_environment.example.json',
                                           'docs/test-cases.md', 'docs/verification-results.md', 'docs/benchmark-results.md',
                                           'docs/screenshots/flow-02-student-reported.png',
                                           'docs/screenshots/flow-07-technician-resolved.png',
                                           'docs/screenshots/flow-09-mailpit-email.png'
        'README and documentation'       = 'README.md', 'docs/requirements.md', 'docs/design.md', 'docs/api-contract.md'
        'Presentation and defense'       = 'docs/presentation.md', 'docs/demo-runbook.md', 'docs/submission-checklist.md'
        'Sources and AI disclosure'      = @('docs/sources-and-contributions.md')
    }
    if ($IncludeSlidesPdf) { $required['Presentation and defense'] += 'docs/presentation.pdf' }

    foreach ($group in $required.GetEnumerator()) {
        $missing = @($group.Value | Where-Object { $_ -notin $files })
        if ($missing) { $failures.Add("$($group.Key): missing $($missing -join ', ')") }
    }
    if (-not ($files -match '^backend/UniversityHelpdesk\.Api/Persistence/Migrations/\d+_\w+\.cs$')) {
        $failures.Add('Database schema and init: no EF Core migrations')
    }
    if (-not ($files -match '^tests/[^/]+/[^/]+\.csproj$')) { $failures.Add('Tests and evidence: no test projects') }
    if ('README.md' -in $files) {
        $readme = Read-Entry 'README.md'
        foreach ($term in 'migrate-database', 'seed-demo-users', 'docker compose up') {
            if (-not $readme.Contains($term)) { $failures.Add("README.md: no '$term' instructions") }
        }
    }
    if ('docs/sources-and-contributions.md' -in $files -and
        -not (Read-Entry 'docs/sources-and-contributions.md').Contains('## AI assistance')) {
        $failures.Add('docs/sources-and-contributions.md: no "AI assistance" section')
    }
    $excluded = [ordered]@{
        'Git metadata'               = '(^|/)\.git(/|$)'
        'Local environment files'    = '(^|/)\.env$|(^|/)\.env\.(?!example$)[^/]+$|(^|/)secrets\.env$|\.postman_environment\.json$'
        'Local secret configuration' = '(^|/)secrets\.json$|\.secrets\.json$|appsettings\.([^/]*\.)?local\.json$'
        'Keys and certificates'      = '\.(pem|key|pfx|p12)$|(^|/)id_(rsa|ed25519)$'
        'Dependencies'               = '(^|/)(node_modules|packages|\.nuget)/'
        'Build output and results'   = '(^|/)(bin|obj|TestResults)/|\.(trx|dll|exe|pdb|nupkg)$|^frontend/(dist|build)/'
        'Editor files'               = '(^|/)(\.vs|\.vscode|\.idea)/|\.(user|suo)$'
        'Nested archives'            = '\.(zip|7z|rar)$'
    }
    foreach ($rule in $excluded.GetEnumerator()) {
        $found = @($files -imatch $rule.Value)
        if ($found) { $failures.Add("$($rule.Key) present: $($found -join ', ')") }
    }

    # Values from the local secret files, compared without printing them.
    $localValues = foreach ($file in (Join-Path $repo '.env'), (Join-Path $repo 'deploy/kubernetes/infrastructure/secrets.env')) {
        if (Test-Path $file) {
            Get-Content $file | Where-Object { $_ -match '^\s*[A-Z0-9_]*(PASSWORD|KEY|SECRET|TOKEN)[A-Z0-9_]*=(.+)$' } |
                ForEach-Object { $Matches[2].Trim() } | Where-Object { $_.Length -ge 8 -and $_ -ne 'change-me' }
        }
    }
    $patterns = [ordered]@{
        'private key'         = '-----BEGIN [A-Z ]*PRIVATE KEY-----'
        'JSON Web Token'      = 'eyJ[A-Za-z0-9_-]{10,}\.eyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}'
        'GitHub token'        = 'gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{30,}'
        'cloud access key'    = 'AKIA[0-9A-Z]{16}'
    }
    $textExtensions = '.cs', '.csproj', '.slnx', '.json', '.md', '.yml', '.yaml', '.ps1', '.sh', '.sql', '.conf',
                      '.example', '.http', '.xml', '.props', '.targets', '.txt', '.gitignore', '.gitattributes', ''
    foreach ($path in $files | Where-Object { [IO.Path]::GetExtension($_) -in $textExtensions }) {
        $text = Read-Entry $path
        foreach ($pattern in $patterns.GetEnumerator()) {
            if ($text -match $pattern.Value) { $failures.Add("Possible $($pattern.Key) in $path") }
        }
        foreach ($value in $localValues) {
            if ($text.Contains($value)) { $failures.Add("A value from the local .env or secrets.env file appears in $path") }
        }
    }

    $size = (Get-Item $zipPath).Length
    $hash = (Get-FileHash $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
    Write-Host "Package: $zipPath"
    Write-Host ("Source: {0}; {1} files; {2:N1} MB; SHA-256 {3}" -f $short, $files.Count, ($size / 1MB), $hash)
    Write-Host ("Added: {0}" -f (($generated.Keys | Sort-Object) -join ', '))
    Write-Host ("Required items: {0} groups checked. Excluded items: {1} rules checked. Text files scanned: {2}; local secret values compared: {3}." -f
        $required.Count, $excluded.Count, @($files | Where-Object { [IO.Path]::GetExtension($_) -in $textExtensions }).Count, @($localValues).Count)
}
finally {
    $zip.Dispose()
}

if ($failures.Count) {
    $failures | Select-Object -Unique | ForEach-Object { Write-Host "FAILED: $_" -ForegroundColor Red }
    exit 1
}
Write-Host 'All package checks passed.' -ForegroundColor Green
