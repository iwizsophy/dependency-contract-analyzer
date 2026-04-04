param(
    [string]$ProjectPath = 'src/DependencyContractAnalyzer/DependencyContractAnalyzer.csproj',
    [string]$Configuration = 'Release',
    [string]$OutputDirectory = 'artifacts',
    [string[]]$AdditionalDotNetPackArguments = @(),
    [string]$SyftPath = 'syft',
    [string]$SyftVersion,
    [switch]$DownloadSyftIfMissing
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.IO.Compression.FileSystem

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

    if ($IsWindows) {
        return "syft_${VersionWithoutPrefix}_windows_amd64.zip"
    }

    if ($IsLinux) {
        return "syft_${VersionWithoutPrefix}_linux_amd64.tar.gz"
    }

    if ($IsMacOS) {
        return "syft_${VersionWithoutPrefix}_darwin_amd64.tar.gz"
    }

    throw 'Syft auto-download is not supported on this operating system.'
}

function Install-SyftIfNeeded {
    param(
        [string]$RequestedSyftPath,
        [string]$RequestedSyftVersion,
        [switch]$AllowDownload
    )

    $resolvedCommand = Get-Command $RequestedSyftPath -ErrorAction SilentlyContinue
    if ($null -ne $resolvedCommand) {
        return $resolvedCommand.Source
    }

    if (-not $AllowDownload.IsPresent) {
        throw "Syft executable '$RequestedSyftPath' was not found on PATH."
    }

    if ([string]::IsNullOrWhiteSpace($RequestedSyftVersion)) {
        throw 'A Syft version is required when DownloadSyftIfMissing is enabled.'
    }

    $versionWithoutPrefix = $RequestedSyftVersion.TrimStart('v')
    $assetName = Get-SyftDownloadAssetName -VersionWithoutPrefix $versionWithoutPrefix
    $downloadUrl = "https://github.com/anchore/syft/releases/download/$RequestedSyftVersion/$assetName"
    $installRoot = Join-Path ([System.IO.Path]::GetTempPath()) "dca-syft-$versionWithoutPrefix"
    $archivePath = Join-Path ([System.IO.Path]::GetTempPath()) $assetName

    if (Test-Path -LiteralPath $installRoot) {
        Remove-Item -LiteralPath $installRoot -Recurse -Force
    }

    New-Item -ItemType Directory -Path $installRoot | Out-Null
    Write-Host "Downloading Syft $RequestedSyftVersion from $downloadUrl"
    Invoke-WebRequest -Uri $downloadUrl -OutFile $archivePath

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

    return $syftExecutablePath
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

Invoke-CommandChecked -ExecutablePath 'dotnet' -Arguments @(
    'pack',
    $resolvedProjectPath,
    '-c',
    $Configuration,
    '-o',
    $resolvedOutputDirectory
) + $AdditionalDotNetPackArguments

$packageFile = Get-PackageFile -PackageOutputDirectory $resolvedOutputDirectory
Add-SbomToPackage -PackageFile $packageFile -ResolvedSyftPath $resolvedSyftPath

Write-Host "Embedded '$script:EmbeddedSbomFileName' into '$($packageFile.FullName)'."
