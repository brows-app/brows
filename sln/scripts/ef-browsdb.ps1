<#
Examples:

    # Create a new migration.
    .\scripts\ef-browsdb.ps1 migrations add NameOfMigration

    # Create a SQL script through a migration in the SQL output directory.
    .\scripts\ef-browsdb.ps1 migrations script NameOfMigration

    # Remove the most recent migration.
    .\scripts\ef-browsdb.ps1 migrations remove

    # Apply migrations to the local LocalDB instance.
    .\scripts\ef-browsdb.ps1 localdb database update

    # Revert the local LocalDB instance to an empty migrations history.
    .\scripts\ef-browsdb.ps1 localdb database update 0
#>
[CmdletBinding()]
param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $EfArguments
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $PSCommandPath
$slnDir = (Resolve-Path (Join-Path $scriptDir '..')).Path
$solutionDir = $slnDir.TrimEnd('\') + '\'
$project = Join-Path $slnDir 'web\source\Brows.Web.DB.EntityFrameworkCore\Brows.Web.DB.EntityFrameworkCore.csproj'
$msbuildProjectExtensionsPath = Join-Path $slnDir 'out\web\obj\Brows.Web.DB.EntityFrameworkCore'
$additionalArguments = @()
$connectionStringEnvironmentVariable = 'BROWSDB_CONNECTIONSTRING'
$connectionString = $null
$migrationsDirectory = Join-Path $slnDir 'web\source\Brows.Web.DB.EntityFrameworkCore\Web\EntityFrameworkCore\Migrations'
$sqlDirectory = Join-Path $slnDir 'web\source\Brows.Web.DB.EntityFrameworkCore\Web\EntityFrameworkCore\sql'
$createSqlPath = Join-Path $sqlDirectory 'create.sql'

function Invoke-Ef {
    param([string[]] $Arguments)

    & dotnet ef `
        --project $project `
        --startup-project $project `
        --framework net10.0 `
        --msbuildprojectextensionspath $msbuildProjectExtensionsPath `
        @Arguments

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet ef $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

if ($EfArguments.Length -gt 0) {
    if ($EfArguments[0] -eq 'localdb') {
        $connectionString = 'Data Source=(LocalDB)\MSSQLLocalDB;Database=brows-db;Integrated Security=true;'
        $EfArguments = @($EfArguments | Select-Object -Skip 1)
    }
    elseif ($EfArguments[0] -eq 'azuresql-prod') {
        $connectionString = 'Server=tcp:brows-sql-01.database.windows.net,1433;Authentication=Active Directory Default;Database=brows-db;'
        $EfArguments = @($EfArguments | Select-Object -Skip 1)
    }
}

if ($EfArguments.Length -ge 2 -and
    $EfArguments[0] -eq 'migrations' -and
    $EfArguments[1] -eq 'add' -and
    $EfArguments -notcontains '--output-dir' -and
    $EfArguments -notcontains '-o') {
    $additionalArguments += '--output-dir'
    $additionalArguments += 'Web\EntityFrameworkCore\Migrations'
}

if ($EfArguments.Length -ge 2 -and
    $EfArguments[0] -eq 'migrations' -and
    $EfArguments[1] -eq 'script' -and
    $EfArguments -notcontains '--output' -and
    $EfArguments -notcontains '-o') {
    $migrationName = 'all'
    if ($EfArguments.Length -ge 4 -and -not $EfArguments[3].StartsWith('-')) {
        $migrationName = $EfArguments[3]
    }
    elseif ($EfArguments.Length -ge 3 -and -not $EfArguments[2].StartsWith('-')) {
        $migrationName = $EfArguments[2]
    }

    $additionalArguments += '--output'
    $additionalArguments += (Join-Path $sqlDirectory "$migrationName.sql")
}

Push-Location $slnDir
try {
    $env:SolutionDir = $solutionDir

    if ($connectionString) {
        Set-Item -Path "Env:$connectionStringEnvironmentVariable" -Value $connectionString
    }

    if ($EfArguments.Length -ge 1 -and $EfArguments[0] -eq 'create-sql') {
        if (Test-Path $migrationsDirectory) {
            throw "Migrations directory already exists at $migrationsDirectory. The 'create-sql' command is only for generating create.sql from a brand-new (empty) migrations history; use 'migrations script' instead when migrations already exist."
        }

        $migrationsCreated = $false
        try {
            Invoke-Ef @('migrations', 'add', 'Initial', '--output-dir', 'Web\EntityFrameworkCore\Migrations')
            $migrationsCreated = $true
            Invoke-Ef @('migrations', 'script', '--output', $createSqlPath)
        }
        finally {
            if ($migrationsCreated -and (Test-Path $migrationsDirectory)) {
                Remove-Item -Path $migrationsDirectory -Recurse -Force
            }
        }

        exit 0
    }

    Invoke-Ef (@($EfArguments) + $additionalArguments)

    exit 0
}
finally {
    Pop-Location
}
