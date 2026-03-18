param(
    [string]$PackageDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($PackageDirectory)) {
    $PackageDirectory = Join-Path (Join-Path $PSScriptRoot '..') 'artifacts'
}

function Get-LatestPackageFile {
    param(
        [string]$PackageDirectoryPath
    )

    $packageFiles = @(
        Get-ChildItem -Path $PackageDirectoryPath -Filter 'DependencyContractAnalyzer.*.nupkg' -File |
            Sort-Object LastWriteTimeUtc -Descending
    )

    if ($packageFiles.Count -eq 0) {
        throw "No packed DependencyContractAnalyzer package was found in '$PackageDirectoryPath'."
    }

    return $packageFiles[0]
}

function Get-PackageVersion {
    param(
        [System.IO.FileInfo]$PackageFile
    )

    if ($PackageFile.Name -notmatch '^DependencyContractAnalyzer\.(?<Version>.+)\.nupkg$') {
        throw "Failed to determine the package version from '$($PackageFile.Name)'."
    }

    return $Matches['Version']
}

function New-NuGetConfig {
    param(
        [string]$ConfigPath,
        [string]$PackageSourcePath,
        [string]$GlobalPackagesFolder
    )

    $escapedPackageSourcePath = [System.Security.SecurityElement]::Escape($PackageSourcePath)
    $escapedGlobalPackagesFolder = [System.Security.SecurityElement]::Escape($GlobalPackagesFolder)

    $configContent = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <config>
    <add key="globalPackagesFolder" value="$escapedGlobalPackagesFolder" />
  </config>
  <packageSources>
    <clear />
    <add key="local" value="$escapedPackageSourcePath" />
  </packageSources>
</configuration>
"@

    Set-Content -Path $ConfigPath -Value $configContent
}

function New-SmokeProject {
    param(
        [string]$ProjectRoot,
        [string]$ProjectName,
        [string]$PackageVersion,
        [string]$SourceCode,
        [switch]$TreatWarningsAsErrors,
        [string]$WarningsAsErrors
    )

    New-Item -ItemType Directory -Path $ProjectRoot | Out-Null

    $propertyLines = @(
        '  <PropertyGroup>',
        '    <TargetFramework>net10.0</TargetFramework>',
        '    <ImplicitUsings>disable</ImplicitUsings>',
        '    <Nullable>enable</Nullable>'
    )

    if ($TreatWarningsAsErrors.IsPresent) {
        $propertyLines += '    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>'
    }

    if (-not [string]::IsNullOrWhiteSpace($WarningsAsErrors)) {
        $propertyLines += "    <WarningsAsErrors>$WarningsAsErrors</WarningsAsErrors>"
    }

    $propertyLines += '  </PropertyGroup>'

    $projectLines = @(
        '<Project Sdk="Microsoft.NET.Sdk">'
    ) + $propertyLines + @(
        '',
        '  <ItemGroup>',
        "    <PackageReference Include=""DependencyContractAnalyzer"" Version=""$PackageVersion"" PrivateAssets=""all"" />",
        '  </ItemGroup>',
        '</Project>'
    )

    $projectPath = Join-Path $ProjectRoot "$ProjectName.csproj"
    Set-Content -Path $projectPath -Value ($projectLines -join [System.Environment]::NewLine)
    Set-Content -Path (Join-Path $ProjectRoot 'ContractUsage.cs') -Value $SourceCode

    return $projectPath
}

function Invoke-DotNet {
    param(
        [string[]]$Arguments,
        [switch]$ExpectFailure,
        [string]$ExpectedOutputFragment
    )

    Write-Host ("dotnet " + ($Arguments -join ' '))
    $outputLines = @(& dotnet @Arguments 2>&1 | ForEach-Object { $_.ToString() })
    $exitCode = $LASTEXITCODE
    $output = [string]::Join([System.Environment]::NewLine, $outputLines)

    if ($ExpectFailure.IsPresent) {
        if ($exitCode -eq 0) {
            throw "Expected 'dotnet $($Arguments -join ' ')' to fail."
        }

        if (-not [string]::IsNullOrWhiteSpace($ExpectedOutputFragment) -and -not $output.Contains($ExpectedOutputFragment, [System.StringComparison]::Ordinal)) {
            throw "Expected 'dotnet $($Arguments -join ' ')' to contain '$ExpectedOutputFragment', but it did not.`n$output"
        }

        return $output
    }

    if ($exitCode -ne 0) {
        throw "Command failed: dotnet $($Arguments -join ' ')`n$output"
    }

    return $output
}

function Assert-FileContains {
    param(
        [string]$Path,
        [string]$ExpectedText
    )

    $content = Get-Content -Path $Path -Raw
    if (-not $content.Contains($ExpectedText, [System.StringComparison]::Ordinal)) {
        throw "Expected '$Path' to contain '$ExpectedText'."
    }
}

$packageDirectoryPath = (Get-Item -LiteralPath $PackageDirectory).FullName
$packageFile = Get-LatestPackageFile -PackageDirectoryPath $packageDirectoryPath
$packageVersion = Get-PackageVersion -PackageFile $packageFile

Write-Host "Using packed package '$($packageFile.FullName)'."

# Keep the smoke project outside the repo so repository-wide MSBuild props do not affect the package test.
$workingRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('dca-packed-package-smoke-' + [System.Guid]::NewGuid().ToString('N'))
$globalPackagesFolder = Join-Path $workingRoot 'packages'
$nuGetConfigPath = Join-Path $workingRoot 'NuGet.Config'

try {
    New-Item -ItemType Directory -Path $workingRoot | Out-Null
    New-Item -ItemType Directory -Path $globalPackagesFolder | Out-Null
    New-NuGetConfig -ConfigPath $nuGetConfigPath -PackageSourcePath $packageDirectoryPath -GlobalPackagesFolder $globalPackagesFolder

    $validSourceCode = @'
using DependencyContractAnalyzer;

[ProvidesContract("thread-safe")]
public interface IClock
{
}

[RequiresDependencyContract(typeof(IClock), "thread-safe")]
public sealed class ValidConsumer
{
    public ValidConsumer(IClock clock)
    {
    }
}
'@

    $validProjectRoot = Join-Path $workingRoot 'ValidConsumer'
    $validProjectPath = New-SmokeProject -ProjectRoot $validProjectRoot -ProjectName 'ValidConsumer' -PackageVersion $packageVersion -SourceCode $validSourceCode -TreatWarningsAsErrors

    Invoke-DotNet -Arguments @('restore', $validProjectPath, '--configfile', $nuGetConfigPath) | Out-Null

    $assetsFilePath = Join-Path $validProjectRoot 'obj/project.assets.json'
    Assert-FileContains -Path $assetsFilePath -ExpectedText '"build/DependencyContractAnalyzer.props"'
    Assert-FileContains -Path $assetsFilePath -ExpectedText '"buildTransitive/DependencyContractAnalyzer.props"'
    Assert-FileContains -Path $assetsFilePath -ExpectedText '"analyzers/dotnet/cs/DependencyContractAnalyzer.dll"'

    Invoke-DotNet -Arguments @('build', $validProjectPath, '--no-restore') | Out-Null

    $invalidSourceCode = @'
using DependencyContractAnalyzer;

public interface IUnreliableClock
{
}

[RequiresDependencyContract(typeof(IUnreliableClock), "thread-safe")]
public sealed class InvalidConsumer
{
    public InvalidConsumer(IUnreliableClock clock)
    {
    }
}
'@

    $invalidProjectRoot = Join-Path $workingRoot 'InvalidConsumer'
    $invalidProjectPath = New-SmokeProject -ProjectRoot $invalidProjectRoot -ProjectName 'InvalidConsumer' -PackageVersion $packageVersion -SourceCode $invalidSourceCode -WarningsAsErrors 'DCA001'

    Invoke-DotNet -Arguments @('restore', $invalidProjectPath, '--configfile', $nuGetConfigPath) | Out-Null
    Invoke-DotNet -Arguments @('build', $invalidProjectPath, '--no-restore') -ExpectFailure -ExpectedOutputFragment 'DCA001' | Out-Null

    Write-Host 'Packed package smoke test passed.'
}
finally {
    if (Test-Path -LiteralPath $workingRoot) {
        Remove-Item -LiteralPath $workingRoot -Recurse -Force
    }
}
