<#
.SYNOPSIS
  Builds and publishes Brows as a .NET Windows application.

.DESCRIPTION
  Builds the complete solution with Visual Studio MSBuild so native projects are included,
  then publishes the Brows application and copies its plugin and native artifacts.

.EXAMPLE
  publish-brows.ps1

.EXAMPLE
  publish-brows.ps1 -OutputPath C:\build\Brows -Configuration Debug
#>
[CmdletBinding()]
param(
    [ValidateNotNullOrEmpty()]
    [string] $OutputPath = (Join-Path -Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)) -ChildPath 'Brows\publish'),

    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',

    [ValidateSet('x64')]
    [string] $Platform = 'x64'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-NativeCommand {
    param(
        [string] $FilePath,
        [string[]] $Arguments
    )

    & $FilePath @Arguments

    if ($LASTEXITCODE -ne 0) {
        throw "'$FilePath' exited with code $LASTEXITCODE."
    }
}

function Test-PathIsEqualOrChild {
    param(
        [string] $Path,
        [string] $Parent
    )

    $normalizedPath = [System.IO.Path]::GetFullPath($Path).TrimEnd('\')
    $normalizedParent = [System.IO.Path]::GetFullPath($Parent).TrimEnd('\')

    return $normalizedPath.Equals($normalizedParent, [System.StringComparison]::OrdinalIgnoreCase) -or
        $normalizedPath.StartsWith("$normalizedParent\", [System.StringComparison]::OrdinalIgnoreCase)
}

function Assert-SafeOutputDirectory {
    param(
        [string] $Path,
        [string[]] $ProtectedPaths
    )

    $root = [System.IO.Path]::GetPathRoot($Path)

    if ($Path.Equals($root, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "The publish output path cannot be a drive root: $Path"
    }

    foreach ($protectedPath in $ProtectedPaths) {
        if ([string]::IsNullOrWhiteSpace($protectedPath)) {
            continue
        }

        if (Test-PathIsEqualOrChild -Path $protectedPath -Parent $Path) {
            throw "The publish output path cannot contain a protected directory: $Path"
        }
    }
}

function Clear-Directory {
    param([string] $Path)

    if (Test-Path -LiteralPath $Path -PathType Leaf) {
        throw "The publish output path is a file: $Path"
    }

    if (Test-Path -LiteralPath $Path -PathType Container) {
        Get-ChildItem -LiteralPath $Path -Force | Remove-Item -Force -Recurse
    }
    else {
        New-Item -ItemType Directory -Path $Path -Force | Out-Null
    }
}

function Copy-Directory {
    param(
        [string] $Source,
        [string] $Destination,
        [string] $Name
    )

    if (-not (Test-Path -LiteralPath $Source -PathType Container)) {
        throw "$Name directory was not produced: $Source"
    }

    New-Item -ItemType Directory -Path $Destination -Force | Out-Null

    foreach ($item in Get-ChildItem -LiteralPath $Source -Force) {
        Copy-Item -LiteralPath $item.FullName -Destination $Destination -Force -Recurse
    }
}

$solutionDirectory = Split-Path -Parent $PSScriptRoot
$repositoryRoot = Split-Path -Parent $solutionDirectory
$solutionPath = Join-Path -Path $solutionDirectory -ChildPath 'brows.slnx'
$applicationProject = Join-Path -Path $solutionDirectory -ChildPath 'app\source\brows\brows.csproj'
$buildOutputDirectory = Join-Path -Path $solutionDirectory -ChildPath "out\$Configuration"
$OutputPath = [System.IO.Path]::GetFullPath($OutputPath)
$publishBuildDirectory = Join-Path -Path ([System.IO.Path]::GetTempPath()) -ChildPath ("BrowsPublishBuild-" + [System.Guid]::NewGuid())

foreach ($requiredPath in @($solutionPath, $applicationProject)) {
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw "Required build input was not found: $requiredPath"
    }
}

$protectedPaths = @(
    $repositoryRoot,
    $solutionDirectory,
    $buildOutputDirectory,
    [Environment]::GetFolderPath([Environment+SpecialFolder]::UserProfile),
    [Environment]::GetFolderPath([Environment+SpecialFolder]::Desktop),
    [Environment]::GetFolderPath([Environment+SpecialFolder]::MyDocuments),
    [Environment]::GetFolderPath([Environment+SpecialFolder]::ApplicationData),
    [Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)
)
Assert-SafeOutputDirectory -Path $OutputPath -ProtectedPaths $protectedPaths

$vswhere = Join-Path -Path ${env:ProgramFiles(x86)} -ChildPath 'Microsoft Visual Studio\Installer\vswhere.exe'

if (-not (Test-Path -LiteralPath $vswhere -PathType Leaf)) {
    throw "Visual Studio locator was not found: $vswhere"
}

$visualStudioPath = (& $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath).Trim()

if ([string]::IsNullOrWhiteSpace($visualStudioPath)) {
    throw 'Visual Studio with the x64 C++ build tools was not found.'
}

$vsDevCmd = Join-Path -Path $visualStudioPath -ChildPath 'Common7\Tools\VsDevCmd.bat'

if (-not (Test-Path -LiteralPath $vsDevCmd -PathType Leaf)) {
    throw "Visual Studio developer command prompt was not found: $vsDevCmd"
}

if ([string]::IsNullOrWhiteSpace($env:ComSpec)) {
    throw 'cmd.exe was not found through the ComSpec environment variable.'
}

$buildCommand = 'call "{0}" -arch={1} -host_arch={1} >nul 2>&1 && msbuild "{2}" /restore /t:Rebuild /p:Configuration={3} /p:Platform={1} /p:Deterministic=true /p:ContinuousIntegrationBuild=true /verbosity:minimal' -f $vsDevCmd, $Platform, $solutionPath, $Configuration
Invoke-NativeCommand -FilePath $env:ComSpec -Arguments @('/d', '/c', $buildCommand)

Clear-Directory -Path $OutputPath

try {
    Invoke-NativeCommand -FilePath 'dotnet' -Arguments @(
        'publish',
        $applicationProject,
        '--output',
        $OutputPath,
        '--configuration',
        $Configuration,
        '--framework',
        'net10.0-windows',
        '--runtime',
        "win-$Platform",
        "-p:SolutionDir=$solutionDirectory\",
        "-p:OutputPath=$publishBuildDirectory\",
        '-p:Deterministic=true',
        '-p:ContinuousIntegrationBuild=true'
    )

    Copy-Directory -Source (Join-Path -Path $buildOutputDirectory -ChildPath 'brows.export') -Destination (Join-Path -Path $OutputPath -ChildPath 'brows.export') -Name 'Plugin'
    Copy-Directory -Source (Join-Path -Path $buildOutputDirectory -ChildPath "brows.native\$Platform-windows") -Destination (Join-Path -Path $OutputPath -ChildPath "brows.native\$Platform-windows") -Name 'Native artifact'
}
finally {
    if (Test-Path -LiteralPath $publishBuildDirectory -PathType Container) {
        Remove-Item -LiteralPath $publishBuildDirectory -Force -Recurse
    }
}

Write-Host "Published Brows to '$OutputPath'."
