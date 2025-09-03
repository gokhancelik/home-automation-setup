# Install kubeseal CLI tool for Windows
# Downloads and installs kubeseal from GitHub releases

param(
    [string]$Version = "v0.26.0",
    [string]$InstallPath = "$env:USERPROFILE\bin"
)

Write-Host "Installing kubeseal $Version..." -ForegroundColor Green

# Ensure install directory exists
if (!(Test-Path $InstallPath)) {
    New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null
}

# Download URL for Windows binary
$downloadUrl = "https://github.com/bitnami-labs/sealed-secrets/releases/download/$Version/kubeseal-$($Version.TrimStart('v'))-windows-amd64.tar.gz"
$tempFile = "$env:TEMP\kubeseal.tar.gz"
$extractPath = "$env:TEMP\kubeseal-extract"

try {
    Write-Host "Downloading kubeseal from $downloadUrl..." -ForegroundColor Yellow
    Invoke-WebRequest -Uri $downloadUrl -OutFile $tempFile

    # Extract the tar.gz file
    Write-Host "Extracting kubeseal..." -ForegroundColor Yellow
    if (Test-Path $extractPath) {
        Remove-Item $extractPath -Recurse -Force
    }
    New-Item -ItemType Directory -Path $extractPath -Force | Out-Null
    
    # Use tar command (available in Windows 10+)
    tar -xzf $tempFile -C $extractPath

    # Move binary to install path
    $binaryPath = "$extractPath\kubeseal.exe"
    if (Test-Path $binaryPath) {
        Copy-Item $binaryPath "$InstallPath\kubeseal.exe" -Force
        Write-Host "kubeseal installed to $InstallPath\kubeseal.exe" -ForegroundColor Green
    } else {
        throw "Binary not found after extraction"
    }

    # Add to PATH if not already there
    $currentPath = [Environment]::GetEnvironmentVariable("PATH", "User")
    if ($currentPath -notlike "*$InstallPath*") {
        Write-Host "Adding $InstallPath to user PATH..." -ForegroundColor Yellow
        [Environment]::SetEnvironmentVariable("PATH", "$currentPath;$InstallPath", "User")
        $env:PATH += ";$InstallPath"
    }

    Write-Host "kubeseal installation completed successfully!" -ForegroundColor Green
    Write-Host "You may need to restart your terminal to use kubeseal from anywhere." -ForegroundColor Yellow

} catch {
    Write-Error "Failed to install kubeseal: $_"
} finally {
    # Cleanup
    if (Test-Path $tempFile) { Remove-Item $tempFile -Force }
    if (Test-Path $extractPath) { Remove-Item $extractPath -Recurse -Force }
}
