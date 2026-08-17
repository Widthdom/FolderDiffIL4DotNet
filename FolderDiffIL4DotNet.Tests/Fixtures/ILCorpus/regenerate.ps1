[CmdletBinding()]
param(
    [string] $Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$fixtureRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$sourceRoot = Join-Path $fixtureRoot "Source"
$projectPath = Join-Path $sourceRoot "ILCorpus.Sample.csproj"
$assemblyPath = Join-Path $sourceRoot "bin/$Configuration/net8.0/ILCorpus.Sample.dll"
$dotnetIldasmOutput = Join-Path $fixtureRoot "dotnet-ildasm-0.12.2.il"
$ilspyOutput = Join-Path $fixtureRoot "ilspycmd-9.1.0.7988.il"

$previousRollForward = $env:DOTNET_ROLL_FORWARD
try {
    $env:DOTNET_ROLL_FORWARD = "Major"

    $dotnetIldasmVersion = (& dotnet-ildasm --version | Out-String)
    if ($dotnetIldasmVersion -notmatch "0\.12\.2\.0") {
        throw "Expected dotnet-ildasm 0.12.2.0, but got: $dotnetIldasmVersion"
    }

    $ilspyVersion = (& ilspycmd --version | Out-String)
    if ($ilspyVersion -notmatch "9\.1\.0\.7988") {
        throw "Expected ilspycmd 9.1.0.7988, but got: $ilspyVersion"
    }

    dotnet build $projectPath --configuration $Configuration --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to build the IL corpus sample assembly."
    }

    dotnet-ildasm $assemblyPath --output $dotnetIldasmOutput --force
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet-ildasm failed."
    }

    & ilspycmd --ilcode --disable-updatecheck $assemblyPath |
        Set-Content -LiteralPath $ilspyOutput -Encoding utf8
    if ($LASTEXITCODE -ne 0) {
        throw "ilspycmd failed."
    }
}
finally {
    $env:DOTNET_ROLL_FORWARD = $previousRollForward
}

Write-Host "Regenerated IL corpus fixtures in $fixtureRoot"
