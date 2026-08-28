#Requires -Version 5.1
<#
.SYNOPSIS
    Bumps the version, builds Release, signs the app EXE, compiles the Inno Setup
    installer, then signs the installer EXE.
.DESCRIPTION
    End-to-end release flow for the OpenDefinery desktop app (modeled on the
    Amzn-Gateway pipeline):
    1. Prompts for (or accepts) the version to deploy.
    2. Writes it into Properties\AssemblyInfo.cs (numeric x.y.z.0) and into the
       AppVersion #define in installer\OpenDefinery.iss.
    3. Builds the solution in Release via MSBuild (.NET Framework / packages.config).
    4. Signs OpenDefinery-DesktopApp.exe in the Release output.
    5. Compiles installer\OpenDefinery.iss with ISCC.exe into dist\.
    6. Signs the resulting installer EXE.

    Requires AZURE_CLIENT_ID, AZURE_CLIENT_SECRET, AZURE_TENANT_ID (Azure Trusted
    Signing), Inno Setup 6, Visual Studio / MSBuild, and the
    Microsoft.Trusted.Signing.Client 1.0.60 package in the NuGet cache.
.PARAMETER Version
    Version to deploy (semver, e.g. 1.0.0 or 1.0.0-beta.1). Prompts if omitted.
.PARAMETER SkipBuild
    Skip the build (re-sign / re-package existing Release binaries).
.EXAMPLE
    PS> .\deploy.ps1
.EXAMPLE
    PS> .\deploy.ps1 -Version 1.0.0
#>

param(
    [string]$Version,
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'

# --- Verify required env vars ---------------------------------------------------
foreach ($var in 'AZURE_CLIENT_ID', 'AZURE_CLIENT_SECRET', 'AZURE_TENANT_ID') {
    if (-not [Environment]::GetEnvironmentVariable($var)) {
        Write-Error "Missing required environment variable: $var"
        exit 1
    }
}

# --- Resolve version ------------------------------------------------------------
# Default to the current installer version (from OpenDefinery.iss) so the user can
# just press Enter to re-use it.
$defaultVersion = $null
$issForDefault = Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) 'OpenDefinery.iss'
if (Test-Path $issForDefault) {
    if ([System.IO.File]::ReadAllText($issForDefault) -match '(?m)^#define\s+AppVersion\s+"([^"]+)"') {
        $defaultVersion = $matches[1]
    }
}

if (-not $Version -and -not $SkipBuild) {
    if ($defaultVersion) {
        $entered = Read-Host "Version to deploy [$defaultVersion]"
        $Version = if ([string]::IsNullOrWhiteSpace($entered)) { $defaultVersion } else { $entered.Trim() }
    } else {
        $Version = Read-Host "Version to deploy (e.g. 1.0.0-beta.1)"
    }
    if (-not $Version) { Write-Error "Version is required."; exit 1 }
}
if ($Version) {
    if ($Version -notmatch '^\d+\.\d+\.\d+(-[0-9A-Za-z.\-]+)?(\+[0-9A-Za-z.\-]+)?$') {
        Write-Error "Invalid version: '$Version'. Expected semver, e.g. 1.2.3 or 1.2.3-beta.4"
        exit 1
    }
}
if ($Version -and $SkipBuild) {
    Write-Warning "-Version was provided with -SkipBuild. Version files will be rewritten but binaries are NOT rebuilt -- make sure the existing binaries match $Version."
}

# --- Paths ----------------------------------------------------------------------
$scriptDir    = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot     = Resolve-Path (Join-Path $scriptDir '..')
$slnFile      = Join-Path $repoRoot 'OpenDefinery-DesktopApp.sln'
$projDir      = Join-Path $repoRoot 'OpenDefinery-DesktopApp'
$asmInfo      = Join-Path $projDir  'Properties\AssemblyInfo.cs'
# SDK-style output now lives under a TFM subfolder.
$releaseExe   = Join-Path $projDir  'bin\Release\net472\OpenDefinery-DesktopApp.exe'
$issFile      = Join-Path $scriptDir 'OpenDefinery.iss'
$metadataJson = Join-Path $repoRoot 'signing-metadata.json'

# Locate ISCC.exe (Inno Setup). Override with ISCC_PATH; otherwise probe common locations.
$iscc = $env:ISCC_PATH
if (-not $iscc) {
    $iscc = @(
        "$env:ProgramFiles\Inno Setup 7\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 7\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
    ) | Where-Object { Test-Path $_ } | Select-Object -First 1
}
if (-not $iscc) { Write-Error "Inno Setup (ISCC.exe) not found. Set ISCC_PATH, or install Inno Setup 6/7."; exit 1 }

# --- Locate MSBuild (via vswhere) ----------------------------------------------
$msbuild = $null
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
if (Test-Path $vswhere) {
    $msbuild = & $vswhere -latest -requires Microsoft.Component.MSBuild `
        -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
}
if (-not $msbuild) { Write-Error "MSBuild not found. Install Visual Studio or Build Tools."; exit 1 }

# --- Locate signtool.exe --------------------------------------------------------
$signTool = $env:SIGNTOOL_PATH
if (-not $signTool) {
    $kitsRoot = (Get-ItemProperty 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows Kits\Installed Roots' `
                    -ErrorAction SilentlyContinue).KitsRoot10
    if ($kitsRoot) {
        $signTool = Get-ChildItem "$kitsRoot\bin" -Filter 'signtool.exe' -Recurse -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -like '*x64*' } |
            Sort-Object LastWriteTime -Descending |
            Select-Object -First 1 -ExpandProperty FullName
    }
}
if (-not $signTool) { $signTool = 'signtool.exe' }

# --- Locate Azure Trusted Signing DLIB ------------------------------------------
$nugetRoot = $env:NUGET_PACKAGES
if (-not $nugetRoot) { $nugetRoot = "$env:USERPROFILE\.nuget\packages" }
$dlib = Join-Path $nugetRoot 'microsoft.trusted.signing.client\1.0.60\bin\x64\Azure.CodeSigning.Dlib.dll'
if (-not (Test-Path $dlib)) {
    Write-Error "Azure.CodeSigning.Dlib.dll not found at: $dlib`nInstall it, e.g.: nuget install Microsoft.Trusted.Signing.Client -Version 1.0.60 (it is also restored by the OpenDefinery-RevitAddins build)."
    exit 1
}

function Invoke-Sign {
    param([string[]]$Files)
    if ($Files.Count -eq 0) { return }
    & $signTool sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 `
        /dlib $dlib /dmdf $metadataJson @Files
    if ($LASTEXITCODE -ne 0) { Write-Error "signtool.exe failed with exit code $LASTEXITCODE"; exit $LASTEXITCODE }
}

# --- Version-bump helpers -------------------------------------------------------
function Update-AssemblyInfoVersion {
    param([Parameter(Mandatory)][string]$Path, [Parameter(Mandatory)][string]$Version)

    # .NET Framework assembly versions must be numeric x.y.z.w -- strip any -prerelease/+build.
    $numeric = ($Version -split '[-+]')[0]
    $parts = [System.Collections.ArrayList]@($numeric.Split('.'))
    while ($parts.Count -lt 4) { [void]$parts.Add('0') }
    $asmVersion = ($parts[0..3] -join '.')

    $content = [System.IO.File]::ReadAllText($Path)
    $content = $content -replace '(?m)^\[assembly:\s*AssemblyVersion\("[^"]*"\)\]',     ('[assembly: AssemblyVersion("'     + $asmVersion + '")]')
    $content = $content -replace '(?m)^\[assembly:\s*AssemblyFileVersion\("[^"]*"\)\]', ('[assembly: AssemblyFileVersion("' + $asmVersion + '")]')
    [System.IO.File]::WriteAllText($Path, $content)

    Write-Host "  AssemblyInfo.cs : -> $asmVersion" -ForegroundColor Green
}

function Update-IssVersion {
    param([Parameter(Mandatory)][string]$Path, [Parameter(Mandatory)][string]$Version)

    $content = [System.IO.File]::ReadAllText($Path)
    $newContent = $content -replace '(?m)^(#define\s+AppVersion\s+")[^"]+(")', ('${1}' + $Version + '${2}')
    [System.IO.File]::WriteAllText($Path, $newContent)

    Write-Host "  OpenDefinery.iss : AppVersion -> $Version" -ForegroundColor Green
}

# --- Step 1: Stamp version -----------------------------------------------------
if ($Version) {
    Write-Host "`nStamping version $Version..." -ForegroundColor Cyan
    Update-AssemblyInfoVersion -Path $asmInfo -Version $Version
    Update-IssVersion          -Path $issFile -Version $Version
}

# --- Step 2: Build Release (SDK-style MSBuild + PackageReference restore) --------
if (-not $SkipBuild) {
    Write-Host "`nRestoring packages..." -ForegroundColor Cyan
    & $msbuild $slnFile -t:restore -v:minimal -nologo
    if ($LASTEXITCODE -ne 0) { Write-Error "Restore failed ($LASTEXITCODE)"; exit $LASTEXITCODE }

    Write-Host "`nBuilding Release..." -ForegroundColor Cyan
    & $msbuild $slnFile -p:Configuration=Release -v:minimal -nologo
    if ($LASTEXITCODE -ne 0) { Write-Error "Build failed ($LASTEXITCODE)"; exit $LASTEXITCODE }
} else {
    Write-Host "`nSkipping build (using existing Release binaries)..." -ForegroundColor Yellow
}

# --- Step 3: Sign our app EXE --------------------------------------------------
if (-not (Test-Path $releaseExe)) {
    Write-Error "Missing build artifact: $releaseExe`nBuild Release first (or drop -SkipBuild)."
    exit 1
}
Write-Host "`nSigning application EXE..." -ForegroundColor Cyan
Write-Host "  $releaseExe"
Invoke-Sign -Files @($releaseExe)

# --- Step 4: Compile installer -------------------------------------------------
Write-Host "`nCompiling installer..." -ForegroundColor Cyan
& $iscc $issFile
if ($LASTEXITCODE -ne 0) { Write-Error "ISCC.exe failed ($LASTEXITCODE)"; exit $LASTEXITCODE }

# --- Step 5: Sign the installer EXE --------------------------------------------
Start-Sleep -Seconds 3   # let Defender finish scanning the new EXE
$distDir = Join-Path $repoRoot 'dist'
if (-not (Test-Path $distDir)) { Write-Error "dist\ folder not found. Check OutputDir in OpenDefinery.iss."; exit 1 }

$exeFile = Get-ChildItem $distDir -Filter '*.exe' |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1 -ExpandProperty FullName
if (-not $exeFile) { Write-Error "No .exe found in $distDir"; exit 1 }

Write-Host "`nSigning installer EXE: $exeFile" -ForegroundColor Cyan
Invoke-Sign -Files @($exeFile)

if ($Version) {
    Write-Host "`nDone. Signed installer for version ${Version}: $exeFile" -ForegroundColor Green
} else {
    Write-Host "`nDone. Signed installer: $exeFile" -ForegroundColor Green
}
