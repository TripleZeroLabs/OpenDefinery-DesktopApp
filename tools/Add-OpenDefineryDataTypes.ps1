<#
.SYNOPSIS
    Logs into the OpenDefinery Drupal backend and adds Revit data types to the
    'data_type' taxonomy.

.DESCRIPTION
    One-off maintenance CLI for seeding new shared-parameter data types into
    OpenDefinery. It mirrors exactly how the desktop app talks to Drupal:

        1. POST /user/login?_format=json          -> CSRF token + session cookie
        2. Basic auth header  = base64(user:pass)  (matches MainWindow.xaml.cs)
        3. GET  /rest/datatypes?_format=json        -> data types already present
        4. POST /taxonomy/term?_format=hal_json     -> create each missing term
           body: {"vid":"data_type","name":[{"value":"<TOKEN>"}]}
           (same shape as Tag.Create in Tag.cs)

    The data types to add are read from a CSV (see -DataTypesFile). Any token
    that already exists in the taxonomy is skipped, so the script is safe to run
    repeatedly.

    SAFETY: by default the script runs as a DRY RUN and only prints what it would
    create. Pass -Execute to actually write to the backend.

.PARAMETER BaseUrl
    Drupal base URL. Defaults to the production app.

.PARAMETER DataTypesFile
    Path to the CSV of data types to add. Defaults to revit-2024-datatypes.csv
    next to this script. CSV needs a 'Token' column; other columns are ignored.

.PARAMETER Vid
    Taxonomy vocabulary machine name. Defaults to 'data_type'.

.PARAMETER Credential
    Optional PSCredential. If omitted you are prompted for username + password.

.PARAMETER Execute
    Actually create the terms. Without this switch the script only previews.

.EXAMPLE
    # Preview what would be added (no changes):
    .\Add-OpenDefineryDataTypes.ps1

.EXAMPLE
    # Log in and create the missing data types:
    .\Add-OpenDefineryDataTypes.ps1 -Execute
#>
[CmdletBinding()]
param(
    [string]$BaseUrl = 'https://app.opendefinery.com',
    [string]$DataTypesFile,
    [string]$Vid = 'data_type',
    [System.Management.Automation.PSCredential]$Credential,
    [switch]$Execute
)

$ErrorActionPreference = 'Stop'
$BaseUrl = $BaseUrl.TrimEnd('/')

function Write-Section($text) { Write-Host ""; Write-Host "== $text ==" -ForegroundColor Cyan }

# Resolve the default CSV path next to this script. ($PSScriptRoot is not
# reliably populated inside a param() default, so do it here.)
if (-not $DataTypesFile) {
    $scriptDir = $PSScriptRoot
    if (-not $scriptDir) { $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path }
    $DataTypesFile = Join-Path $scriptDir 'revit-2024-datatypes.csv'
}

# --- 1. Read the list of data types to add ------------------------------------
if (-not (Test-Path $DataTypesFile)) {
    throw "Data types file not found: $DataTypesFile"
}

# Strip comment/blank lines, then parse as CSV.
$csvLines = Get-Content -LiteralPath $DataTypesFile |
    Where-Object { $_.Trim() -ne '' -and -not $_.TrimStart().StartsWith('#') }
$wanted = ($csvLines | ConvertFrom-Csv) |
    Where-Object { $_.Token -and $_.Token.Trim() -ne '' } |
    ForEach-Object { $_.Token.Trim() }

if (-not $wanted) { throw "No data type tokens found in $DataTypesFile" }
Write-Host "Loaded $($wanted.Count) candidate data type(s) from $DataTypesFile"

# --- 2. Gather credentials -----------------------------------------------------
# Inline console prompts (avoids Get-Credential's GUI dialog, which can open
# hidden behind the terminal). Pass -Credential to skip the prompts entirely.
if ($Credential) {
    $username = $Credential.UserName
    $password = $Credential.GetNetworkCredential().Password
}
else {
    Write-Section "OpenDefinery login for $BaseUrl"
    $username = Read-Host "Username"
    $securePass = Read-Host "Password" -AsSecureString
    $password = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto(
        [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePass))
}
if (-not $username -or -not $password) { throw "Username and password are required." }

# Basic auth code (ISO-8859-1, matching the desktop app).
$basic = [Convert]::ToBase64String(
    [Text.Encoding]::GetEncoding('ISO-8859-1').GetBytes("${username}:${password}"))

# --- 3. Log in -----------------------------------------------------------------
Write-Section "Logging in as '$username'"
$loginBody = @{ name = $username; pass = $password } | ConvertTo-Json
try {
    $login = Invoke-RestMethod -Method Post -Uri "$BaseUrl/user/login?_format=json" `
        -ContentType 'application/json' -Body $loginBody -SessionVariable session
}
catch {
    throw "Login failed: $($_.Exception.Message)"
}
$csrf = $login.csrf_token
if (-not $csrf) { throw "Login succeeded but no CSRF token was returned." }
Write-Host "Logged in as $($login.current_user.name) (uid $($login.current_user.uid))" -ForegroundColor Green

$headers = @{
    'X-CSRF-Token'  = $csrf
    'Authorization' = "Basic $basic"
}

# --- 4. Fetch existing data types so we don't create duplicates ----------------
Write-Section "Reading existing data types"
$existing = Invoke-RestMethod -Method Get -Uri "$BaseUrl/rest/datatypes?_format=json" `
    -Headers $headers -WebSession $session
$existingNames = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
foreach ($d in $existing) { [void]$existingNames.Add([string]$d.name) }
Write-Host "Backend currently has $($existingNames.Count) data type(s)."

$toCreate = $wanted | Where-Object { -not $existingNames.Contains($_) } | Select-Object -Unique
$alreadyThere = $wanted | Where-Object { $existingNames.Contains($_) }

if ($alreadyThere) {
    Write-Host "Skipping $($alreadyThere.Count) already present: $($alreadyThere -join ', ')" -ForegroundColor DarkGray
}

if (-not $toCreate) {
    Write-Host "`nNothing to do - every candidate already exists." -ForegroundColor Green
    return
}

Write-Section "$($toCreate.Count) data type(s) to create"
$toCreate | ForEach-Object { Write-Host "  + $_" }

if (-not $Execute) {
    Write-Host "`nDRY RUN - no changes made. Re-run with -Execute to create these." -ForegroundColor Yellow
    return
}

# --- 5. Create the missing terms ----------------------------------------------
Write-Section "Creating terms"
$created = @(); $failed = @()
foreach ($token in $toCreate) {
    $body = @{
        vid  = $Vid
        name = @(@{ value = $token })
    } | ConvertTo-Json -Depth 5

    try {
        $resp = Invoke-RestMethod -Method Post -Uri "$BaseUrl/taxonomy/term?_format=hal_json" `
            -Headers $headers -ContentType 'application/json' -Body $body -WebSession $session
        $tid = $resp.tid[0].value
        Write-Host ("  created {0,-40} tid={1}" -f $token, $tid) -ForegroundColor Green
        $created += [pscustomobject]@{ Token = $token; Tid = $tid }
    }
    catch {
        Write-Host ("  FAILED  {0,-40} {1}" -f $token, $_.Exception.Message) -ForegroundColor Red
        $failed += $token
    }
}

# --- 6. Summary ----------------------------------------------------------------
Write-Section "Summary"
Write-Host "Created: $($created.Count)" -ForegroundColor Green
if ($failed) { Write-Host "Failed:  $($failed.Count) -> $($failed -join ', ')" -ForegroundColor Red }
Write-Host "`nReload the desktop app (or re-open the new-parameter form) to see the new data types in NewParamDataTypeCombo."
