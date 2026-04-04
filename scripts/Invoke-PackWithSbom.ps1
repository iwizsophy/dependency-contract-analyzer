param(
    [string]$ProjectPath = 'src/DependencyContractAnalyzer/DependencyContractAnalyzer.csproj',
    [string]$Configuration = 'Release',
    [string]$OutputDirectory = 'artifacts',
    [switch]$NoBuild,
    [switch]$NoRestore,
    [string[]]$PackProperties = @(),
    [string]$SyftPath = 'syft',
    [string]$SyftVersion,
    [switch]$DownloadSyftIfMissing
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.Runtime

$script:EmbeddedSbomFileName = 'sbom.cdx.json'

function Resolve-AbsolutePath {
    param(
        [string]$Path
    )

    return [System.IO.Path]::GetFullPath((Join-Path (Get-Location) $Path))
}

function Invoke-CommandChecked {
    param(
        [string]$ExecutablePath,
        [string[]]$Arguments
    )

    Write-Host ($ExecutablePath + ' ' + ($Arguments -join ' '))
    & $ExecutablePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed: $ExecutablePath $($Arguments -join ' ')"
    }
}

function Get-SyftDownloadAssetName {
    param(
        [string]$VersionWithoutPrefix
    )

    $architectureSuffix = switch ([System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture) {
        ([System.Runtime.InteropServices.Architecture]::X64) { 'amd64' }
        ([System.Runtime.InteropServices.Architecture]::Arm64) { 'arm64' }
        default { throw "Syft auto-download is not supported for process architecture '$([System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture)'." }
    }

    if ($IsWindows) {
        return "syft_${VersionWithoutPrefix}_windows_${architectureSuffix}.zip"
    }

    if ($IsLinux) {
        return "syft_${VersionWithoutPrefix}_linux_${architectureSuffix}.tar.gz"
    }

    if ($IsMacOS) {
        return "syft_${VersionWithoutPrefix}_darwin_${architectureSuffix}.tar.gz"
    }

    throw 'Syft auto-download is not supported on this operating system.'
}

function Get-SyftVersion {
    param(
        [string]$ExecutablePath
    )

    $versionOutput = & $ExecutablePath 'version' '--output' 'json' 2>$null
    if ($LASTEXITCODE -eq 0) {
        try {
            $parsedVersion = $versionOutput | ConvertFrom-Json
            if (-not [string]::IsNullOrWhiteSpace($parsedVersion.version)) {
                return [string]$parsedVersion.version
            }
        }
        catch {
        }
    }

    $versionOutput = & $ExecutablePath 'version' 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to determine the version of Syft executable '$ExecutablePath'."
    }

    foreach ($outputLine in $versionOutput) {
        if ($outputLine -match 'Version:\s*(?<Version>\S+)') {
            return $Matches['Version']
        }
    }

    throw "Failed to parse the version of Syft executable '$ExecutablePath'."
}

function Get-FileSha256 {
    param(
        [string]$Path
    )

    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Assert-DownloadedFileMatchesChecksum {
    param(
        [string]$ArchivePath,
        [string]$ChecksumsPath,
        [string]$AssetName
    )

    $expectedChecksum = $null
    foreach ($checksumLine in Get-Content -LiteralPath $ChecksumsPath) {
        $trimmedLine = $checksumLine.Trim()
        if ([string]::IsNullOrWhiteSpace($trimmedLine)) {
            continue
        }

        if ($trimmedLine -match '^(?<Checksum>[0-9A-Fa-f]{64})\s+[* ](?<Name>.+)$' -and $Matches['Name'] -eq $AssetName) {
            $expectedChecksum = $Matches['Checksum'].ToLowerInvariant()
            break
        }
    }

    if ($null -eq $expectedChecksum) {
        throw "Failed to locate checksum entry for '$AssetName' in '$ChecksumsPath'."
    }

    $actualChecksum = Get-FileSha256 -Path $ArchivePath
    if ($actualChecksum -ne $expectedChecksum) {
        throw "Checksum verification failed for '$AssetName'. Expected '$expectedChecksum', got '$actualChecksum'."
    }
}

function Install-SyftIfNeeded {
    param(
        [string]$RequestedSyftPath,
        [string]$RequestedSyftVersion,
        [switch]$AllowDownload
    )

    $resolvedCommand = Get-Command $RequestedSyftPath -ErrorAction SilentlyContinue
    if ($null -ne $resolvedCommand) {
        if ([string]::IsNullOrWhiteSpace($RequestedSyftVersion)) {
            return $resolvedCommand.Source
        }

        $resolvedVersion = Get-SyftVersion -ExecutablePath $resolvedCommand.Source
        if ($resolvedVersion -eq $RequestedSyftVersion.TrimStart('v')) {
            return $resolvedCommand.Source
        }

        Write-Host "Found Syft '$resolvedVersion' on PATH, but '$RequestedSyftVersion' is required. Downloading the pinned release."
    }

    if (-not $AllowDownload.IsPresent) {
        throw "Syft executable '$RequestedSyftPath' was not found on PATH."
    }

    if ([string]::IsNullOrWhiteSpace($RequestedSyftVersion)) {
        throw 'A Syft version is required when DownloadSyftIfMissing is enabled.'
    }

    $versionWithoutPrefix = $RequestedSyftVersion.TrimStart('v')
    $assetName = Get-SyftDownloadAssetName -VersionWithoutPrefix $versionWithoutPrefix
    $checksumsFileName = "syft_${versionWithoutPrefix}_checksums.txt"
    $downloadUrl = "https://github.com/anchore/syft/releases/download/$RequestedSyftVersion/$assetName"
    $checksumsUrl = "https://github.com/anchore/syft/releases/download/$RequestedSyftVersion/$checksumsFileName"
    $tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("dca-syft-$versionWithoutPrefix-" + [System.Guid]::NewGuid().ToString('N'))
    $installRoot = Join-Path $tempRoot 'install'
    $archivePath = Join-Path $tempRoot $assetName
    $checksumsPath = Join-Path $tempRoot $checksumsFileName

    New-Item -ItemType Directory -Path $installRoot -Force | Out-Null

    try {
        Write-Host "Downloading Syft $RequestedSyftVersion from $downloadUrl"
        Invoke-WebRequest -Uri $downloadUrl -OutFile $archivePath
        Invoke-WebRequest -Uri $checksumsUrl -OutFile $checksumsPath
        Assert-DownloadedFileMatchesChecksum -ArchivePath $archivePath -ChecksumsPath $checksumsPath -AssetName $assetName

        if ($IsWindows) {
            Expand-Archive -Path $archivePath -DestinationPath $installRoot -Force
            $syftExecutablePath = Join-Path $installRoot 'syft.exe'
        }
        else {
            Invoke-CommandChecked -ExecutablePath 'tar' -Arguments @('-xzf', $archivePath, '-C', $installRoot)
            $syftExecutablePath = Join-Path $installRoot 'syft'
        }

        if (-not (Test-Path -LiteralPath $syftExecutablePath)) {
            throw "Syft executable was not found after extraction: $syftExecutablePath"
        }

        $downloadedVersion = Get-SyftVersion -ExecutablePath $syftExecutablePath
        if ($downloadedVersion -ne $versionWithoutPrefix) {
            throw "Downloaded Syft version '$downloadedVersion' does not match requested version '$RequestedSyftVersion'."
        }

        return $syftExecutablePath
    }
    finally {
        if (Test-Path -LiteralPath $archivePath) {
            Remove-Item -LiteralPath $archivePath -Force
        }

        if (Test-Path -LiteralPath $checksumsPath) {
            Remove-Item -LiteralPath $checksumsPath -Force
        }
    }
}

function Get-PackageFile {
    param(
        [string]$PackageOutputDirectory
    )

    $packageFiles = @(
        Get-ChildItem -Path $PackageOutputDirectory -Filter 'DependencyContractAnalyzer.*.nupkg' -File
    )

    if ($packageFiles.Count -eq 0) {
        throw "No packed DependencyContractAnalyzer package was found in '$PackageOutputDirectory'."
    }

    if ($packageFiles.Count -gt 1) {
        $packageList = $packageFiles | ForEach-Object { $_.Name } | Sort-Object
        throw "Expected exactly one packed DependencyContractAnalyzer package in '$PackageOutputDirectory', but found multiple:`n$($packageList -join [System.Environment]::NewLine)"
    }

    return $packageFiles[0]
}

function Add-SbomToPackage {
    param(
        [System.IO.FileInfo]$PackageFile,
        [string]$ResolvedSyftPath
    )

    $workingRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('dca-sbom-' + [System.Guid]::NewGuid().ToString('N'))
    $extractRoot = Join-Path $workingRoot 'package'
    $sbomWorkingPath = Join-Path $workingRoot $script:EmbeddedSbomFileName
    $repackedArchivePath = Join-Path $workingRoot 'repacked.zip'

    try {
        New-Item -ItemType Directory -Path $extractRoot -Force | Out-Null
        [System.IO.Compression.ZipFile]::ExtractToDirectory($PackageFile.FullName, $extractRoot)

        if ($PackageFile.Name -notmatch '^(?<PackageId>DependencyContractAnalyzer)\.(?<PackageVersion>.+)\.nupkg$') {
            throw "Failed to determine the package identity from '$($PackageFile.Name)'."
        }

        $packageId = $Matches['PackageId']
        $packageVersion = $Matches['PackageVersion']

        Invoke-CommandChecked -ExecutablePath $ResolvedSyftPath -Arguments @(
            'scan',
            "dir:$extractRoot",
            '--source-name',
            $packageId,
            '--source-version',
            $packageVersion,
            '-o',
            "cyclonedx-json=$sbomWorkingPath"
        )

        if (-not (Test-Path -LiteralPath $sbomWorkingPath)) {
            throw "Syft did not generate an SBOM file at '$sbomWorkingPath'."
        }

        Move-Item -LiteralPath $sbomWorkingPath -Destination (Join-Path $extractRoot $script:EmbeddedSbomFileName)

        if (Test-Path -LiteralPath $repackedArchivePath) {
            Remove-Item -LiteralPath $repackedArchivePath -Force
        }

        [System.IO.Compression.ZipFile]::CreateFromDirectory(
            $extractRoot,
            $repackedArchivePath,
            [System.IO.Compression.CompressionLevel]::Optimal,
            $false)

        Move-Item -LiteralPath $repackedArchivePath -Destination $PackageFile.FullName -Force
    }
    finally {
        if (Test-Path -LiteralPath $workingRoot) {
            Remove-Item -LiteralPath $workingRoot -Recurse -Force
        }
    }
}

$resolvedProjectPath = Resolve-AbsolutePath -Path $ProjectPath
$resolvedOutputDirectory = Resolve-AbsolutePath -Path $OutputDirectory
$resolvedSyftPath = Install-SyftIfNeeded -RequestedSyftPath $SyftPath -RequestedSyftVersion $SyftVersion -AllowDownload:$DownloadSyftIfMissing

if (-not (Test-Path -LiteralPath $resolvedOutputDirectory)) {
    New-Item -ItemType Directory -Path $resolvedOutputDirectory | Out-Null
}

$dotNetPackArguments = @(
    'pack',
    $resolvedProjectPath,
    '-c',
    $Configuration,
    '-o',
    $resolvedOutputDirectory
)

if ($NoBuild.IsPresent) {
    $dotNetPackArguments += '--no-build'
}

if ($NoRestore.IsPresent) {
    $dotNetPackArguments += '--no-restore'
}

foreach ($packProperty in $PackProperties) {
    $dotNetPackArguments += "-p:$packProperty"
}

Invoke-CommandChecked -ExecutablePath 'dotnet' -Arguments $dotNetPackArguments

$packageFile = Get-PackageFile -PackageOutputDirectory $resolvedOutputDirectory
Add-SbomToPackage -PackageFile $packageFile -ResolvedSyftPath $resolvedSyftPath

Write-Host "Embedded '$script:EmbeddedSbomFileName' into '$($packageFile.FullName)'."
