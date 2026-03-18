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
        Get-ChildItem -Path $PackageDirectoryPath -Filter 'DependencyContractAnalyzer.*.nupkg' -File
    )

    if ($packageFiles.Count -eq 0) {
        throw "No packed DependencyContractAnalyzer package was found in '$PackageDirectoryPath'."
    }

    if ($packageFiles.Count -gt 1) {
        $packageList = $packageFiles | ForEach-Object { $_.Name } | Sort-Object
        throw "Expected exactly one packed DependencyContractAnalyzer package in '$PackageDirectoryPath', but found multiple:`n$($packageList -join [System.Environment]::NewLine)"
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
        [string]$TargetFramework,
        [string]$SourceCode,
        [switch]$TreatWarningsAsErrors,
        [string]$WarningsAsErrors
    )

    New-Item -ItemType Directory -Path $ProjectRoot | Out-Null

    $propertyLines = @(
        '  <PropertyGroup>',
        "    <TargetFramework>$TargetFramework</TargetFramework>",
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

function New-GlobalJson {
    param(
        [string]$ProjectRoot,
        [string]$SdkVersion
    )

    $globalJsonContent = @"
{
  "sdk": {
    "version": "$SdkVersion",
    "rollForward": "latestPatch"
  }
}
"@

    Set-Content -Path (Join-Path $ProjectRoot 'global.json') -Value $globalJsonContent
}

function Invoke-DotNet {
    param(
        [string]$WorkingDirectory,
        [string[]]$Arguments,
        [switch]$ExpectFailure,
        [string]$ExpectedOutputFragment
    )

    Write-Host ("dotnet " + ($Arguments -join ' '))
    Push-Location $WorkingDirectory
    try {
        $outputLines = @(& dotnet @Arguments 2>&1 | ForEach-Object { $_.ToString() })
        $exitCode = $LASTEXITCODE
        $output = [string]::Join([System.Environment]::NewLine, $outputLines)
    }
    finally {
        Pop-Location
    }

    if ($ExpectFailure.IsPresent) {
        if ($exitCode -eq 0) {
            throw "Expected 'dotnet $($Arguments -join ' ')' to fail."
        }

        if (-not [string]::IsNullOrWhiteSpace($ExpectedOutputFragment) -and $output.IndexOf($ExpectedOutputFragment, [System.StringComparison]::Ordinal) -lt 0) {
            throw "Expected 'dotnet $($Arguments -join ' ')' to contain '$ExpectedOutputFragment', but it did not.`n$output"
        }

        return $output
    }

    if ($exitCode -ne 0) {
        throw "Command failed: dotnet $($Arguments -join ' ')`n$output"
    }

    return $output
}

function Assert-TextDoesNotContain {
    param(
        [string]$Content,
        [string]$UnexpectedText,
        [string]$Context
    )

    if ($Content.IndexOf($UnexpectedText, [System.StringComparison]::Ordinal) -ge 0) {
        throw "Did not expect '$UnexpectedText' in $Context.`n$Content"
    }
}

function Assert-SelectedSdkMatchesLine {
    param(
        [string]$Content,
        [string]$ExpectedSdkVersion,
        [string]$Context
    )

    $selectedSdkVersionText = $Content.Trim()
    $selectedSdkVersion = [System.Version]::Parse($selectedSdkVersionText)
    $expectedSdkVersionValue = [System.Version]::Parse($ExpectedSdkVersion)

    if ($selectedSdkVersion.Major -ne $expectedSdkVersionValue.Major -or
        $selectedSdkVersion.Minor -ne $expectedSdkVersionValue.Minor) {
        throw "Expected $Context to use SDK line '$($expectedSdkVersionValue.Major).$($expectedSdkVersionValue.Minor)', but got:`n$Content"
    }
}

function Assert-FileContains {
    param(
        [string]$Path,
        [string]$ExpectedText
    )

    $content = Get-Content -Path $Path -Raw
    if ($content.IndexOf($ExpectedText, [System.StringComparison]::Ordinal) -lt 0) {
        throw "Expected '$Path' to contain '$ExpectedText'."
    }
}

function Get-InstalledSdkVersionForMajor {
    param(
        [int]$MajorVersion
    )

    $sdkLine = dotnet --list-sdks |
        ForEach-Object { $_.ToString().Trim() } |
        Where-Object { $_ -match "^(?<Version>$MajorVersion\.\d+\.\d+)\s+\[" } |
        Select-Object -First 1

    if ($null -eq $sdkLine) {
        throw "Required .NET SDK major version '$MajorVersion' is not installed."
    }

    if ($sdkLine -notmatch '^(?<Version>\d+\.\d+\.\d+)\s+\[') {
        throw "Failed to parse SDK version from '$sdkLine'."
    }

    return $Matches['Version']
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

    $smokeTargets = @(
        @{ TargetFramework = 'net8.0'; SdkMajor = 8 },
        @{ TargetFramework = 'net9.0'; SdkMajor = 9 },
        @{ TargetFramework = 'net10.0'; SdkMajor = 10 }
    )

    foreach ($smokeTarget in $smokeTargets) {
        $targetFramework = [string]$smokeTarget.TargetFramework
        $sdkMajor = [int]$smokeTarget.SdkMajor
        $sdkVersion = Get-InstalledSdkVersionForMajor -MajorVersion $sdkMajor

        Write-Host "Smoke-testing packed package on $targetFramework with SDK $sdkVersion."

        $validProjectRoot = Join-Path $workingRoot "ValidConsumer.$targetFramework"
        $validProjectPath = New-SmokeProject `
            -ProjectRoot $validProjectRoot `
            -ProjectName 'ValidConsumer' `
            -PackageVersion $packageVersion `
            -TargetFramework $targetFramework `
            -SourceCode $validSourceCode `
            -TreatWarningsAsErrors
        New-GlobalJson -ProjectRoot $validProjectRoot -SdkVersion $sdkVersion

        $selectedValidSdkVersion = Invoke-DotNet -WorkingDirectory $validProjectRoot -Arguments @('--version')
        Assert-SelectedSdkMatchesLine -Content $selectedValidSdkVersion -ExpectedSdkVersion $sdkVersion -Context "dotnet --version output for $targetFramework valid project"

        Invoke-DotNet -WorkingDirectory $validProjectRoot -Arguments @('restore', $validProjectPath, '--configfile', $nuGetConfigPath) | Out-Null

        $assetsFilePath = Join-Path $validProjectRoot 'obj/project.assets.json'
        Assert-FileContains -Path $assetsFilePath -ExpectedText '"build/DependencyContractAnalyzer.props"'
        Assert-FileContains -Path $assetsFilePath -ExpectedText '"buildTransitive/DependencyContractAnalyzer.props"'
        Assert-FileContains -Path $assetsFilePath -ExpectedText '"analyzers/dotnet/cs/DependencyContractAnalyzer.dll"'

        $validBuildOutput = Invoke-DotNet -WorkingDirectory $validProjectRoot -Arguments @('build', $validProjectPath, '--no-restore')
        Assert-TextDoesNotContain -Content $validBuildOutput -UnexpectedText 'CS9057' -Context "successful build output for $targetFramework"

        $invalidProjectRoot = Join-Path $workingRoot "InvalidConsumer.$targetFramework"
        $invalidProjectPath = New-SmokeProject `
            -ProjectRoot $invalidProjectRoot `
            -ProjectName 'InvalidConsumer' `
            -PackageVersion $packageVersion `
            -TargetFramework $targetFramework `
            -SourceCode $invalidSourceCode `
            -WarningsAsErrors 'DCA001'
        New-GlobalJson -ProjectRoot $invalidProjectRoot -SdkVersion $sdkVersion

        $selectedInvalidSdkVersion = Invoke-DotNet -WorkingDirectory $invalidProjectRoot -Arguments @('--version')
        Assert-SelectedSdkMatchesLine -Content $selectedInvalidSdkVersion -ExpectedSdkVersion $sdkVersion -Context "dotnet --version output for $targetFramework invalid project"

        Invoke-DotNet -WorkingDirectory $invalidProjectRoot -Arguments @('restore', $invalidProjectPath, '--configfile', $nuGetConfigPath) | Out-Null
        $invalidBuildOutput = Invoke-DotNet -WorkingDirectory $invalidProjectRoot -Arguments @('build', $invalidProjectPath, '--no-restore') -ExpectFailure -ExpectedOutputFragment 'DCA001'
        Assert-TextDoesNotContain -Content $invalidBuildOutput -UnexpectedText 'CS9057' -Context "failing build output for $targetFramework"
    }

    Write-Host 'Packed package smoke test passed.'
}
finally {
    if (Test-Path -LiteralPath $workingRoot) {
        Remove-Item -LiteralPath $workingRoot -Recurse -Force
    }
}
