Write-Host "Setting up Native Dependencies..."

$DepsDir = Join-Path $PSScriptRoot "NativeCore\deps"
if (-not (Test-Path $DepsDir)) { New-Item -ItemType Directory -Force -Path $DepsDir | Out-Null }

# 1. Download libobs binaries (Using the same source as the previous tool, or a known reliable one)
# We need headers and libs. The 'standard' zip from github often lacks .lib files for linking.
# We will check if we can get a dev package.
# For this transition demo, we assume the user has OBS installed or we use a specific known dev release.
# Let's use a placeholder approach: The user must provide 'libobs' in deps.
# OR we can try to download the 'pdb' zip which sometimes has libs? No.
# Standard practice: Users build OBS from source or use 'CI artifacts'.
# We will download a known artifact if possible.

# URL for OBS Studio 29.1.3 (Example) - We need the "Full" zip or similar.
# Actually, linking against 'libobs.dll' directly requires generating .lib.
# We will assume the user has the SDK or will perform the dumpbin step.
# For now, create the folders so CMake doesn't error immediately.

$LibObsDir = Join-Path $DepsDir "libobs"
New-Item -ItemType Directory -Force -Path "$LibObsDir\include" | Out-Null
New-Item -ItemType Directory -Force -Path "$LibObsDir\bin\64bit" | Out-Null

# 2. Download libmpv
$LibMpvDir = Join-Path $DepsDir "libmpv"
if (-not (Test-Path $LibMpvDir)) {
    Write-Host "Downloading libmpv..."
    # shinchiro build dev package
    $MpvUrl = "https://github.com/shinchiro/mpv-winbuild-cmake/releases/download/20260122/mpv-dev-x86_64-v3-20260122-git-6e54aa3.7z"
    $Archive = Join-Path $DepsDir "mpv-dev.7z"
    Invoke-WebRequest -Uri $MpvUrl -OutFile $Archive

    # Extract (Assuming 7z is in path or we use a tool)
    # Since we are in PowerShell, we can try using 7z if available, or tell user.
    Write-Host "Please extract $Archive to $LibMpvDir manually if you don't have 7z in PATH."
    # Attempt extraction if 7z exists
    try {
        & 7z x $Archive -o"$LibMpvDir"
    } catch {
        Write-Host "Extraction failed. Please extract manually."
    }
}

Write-Host "Dependencies setup initiated. Please ensure 'libobs' includes headers and libraries in $LibObsDir."
