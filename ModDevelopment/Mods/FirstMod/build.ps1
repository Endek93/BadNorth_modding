param([switch] $Deploy)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'FirstMod.csproj'
$referencePackages = Join-Path $PSScriptRoot '../../Tools/ReferencePackages'
$packageCache = Join-Path $PSScriptRoot 'obj/packages'

dotnet build $project -c Release --nologo --source $referencePackages "-p:RestorePackagesPath=$packageCache" -p:NuGetAudit=false
if ($LASTEXITCODE -ne 0) {
    throw "Build failed with exit code $LASTEXITCODE."
}

if ($Deploy) {
    if (Get-Process -Name BadNorth -ErrorAction SilentlyContinue) {
        throw 'Close Bad North before deploying the plugin.'
    }

    $pluginDll = Join-Path $PSScriptRoot 'bin/Release/net35/FirstMod.dll'
    $pluginDirectory = Join-Path $PSScriptRoot '../../../BepInEx/plugins/FirstMod'
    New-Item -ItemType Directory -Path $pluginDirectory -Force | Out-Null
    Copy-Item -LiteralPath $pluginDll -Destination $pluginDirectory
    Get-Item -LiteralPath (Join-Path $pluginDirectory 'FirstMod.dll')
}
