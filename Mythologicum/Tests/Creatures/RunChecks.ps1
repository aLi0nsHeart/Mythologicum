param(
    [string]$UnityEditorData = 'D:\MeineProgramme\Unity\6000.0.74f1\Editor\Data'
)

$ErrorActionPreference = 'Stop'
$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$originalMonoPath = $env:MONO_PATH
$locationPushed = $false

try {
    Push-Location -LiteralPath $projectRoot
    $locationPushed = $true
    $netCoreRunner = Join-Path $UnityEditorData 'Tools\netcorerun\netcorerun.exe'
    $compiler = Join-Path $UnityEditorData 'DotNetSdkRoslyn\csc.dll'
    $mono = Join-Path $UnityEditorData 'MonoBleedingEdge\bin\mono.exe'
    $unityAssemblies = Join-Path $UnityEditorData 'Managed\UnityEngine'
    foreach ($requiredPath in @($netCoreRunner, $compiler, $mono, $unityAssemblies)) {
        if (-not (Test-Path -LiteralPath $requiredPath)) {
            throw "Required Unity tool missing: $requiredPath. Pass -UnityEditorData with the Editor\Data folder."
        }
    }

    # Unity's response file supplies the project's actual references and build defines.
    $responseFile = Get-ChildItem -LiteralPath 'Library\Bee\artifacts' -Recurse -File -Filter 'Assembly-CSharp.rsp' |
        Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
    if ($null -eq $responseFile) {
        throw 'No Unity compiler response file found. Open this project in Unity and let its scripts compile first.'
    }
    $options = @(Get-Content -LiteralPath $responseFile.FullName | Where-Object {
        $_ -notmatch '^\s*[-/](out|refout|target):' -and $_ -notmatch '^\s*".*\.cs"\s*$'
    })

    # Include new runtime files even when Unity's cached source list predates them.
    $sources = @(Get-ChildItem -LiteralPath 'Assets' -Recurse -File -Filter '*.cs' |
        Where-Object { $_.FullName -notmatch '[\\/]Editor[\\/]' } |
        Sort-Object FullName | ForEach-Object { '"' + $_.FullName + '"' })
    if ($sources.Count -eq 0) { throw 'No runtime C# sources found in Assets.' }
    $checks = @('CreatureModelChecks', 'CreatureNavigationTests', 'GrazingFieldChecks', 'BuildingOperatingChecks',
        'CreatureWellbeingChecks', 'EnvironmentTreeChecks', 'CreatureEnergyChecks', 'FenceClimbingChecks')
    $checks += @('..\Visitors\VisitorModelChecks', '..\Visitors\VisitorNavigationChecks', '..\Visitors\EconomyChecks')
    $outputFolder = Join-Path $projectRoot 'Temp\CreatureChecks'
    $null = New-Item -ItemType Directory -Path $outputFolder -Force
    $env:MONO_PATH = $unityAssemblies
    if ($originalMonoPath) { $env:MONO_PATH += [System.IO.Path]::PathSeparator + $originalMonoPath }

    foreach ($check in $checks) {
        $testSource = Join-Path $PSScriptRoot ($check + '.cs')
        $checkName = Split-Path -Leaf $check
        $executable = Join-Path $outputFolder ($checkName + '.exe')
        $testResponse = Join-Path $outputFolder ($checkName + '.rsp')
        $arguments = $options + @('-target:exe', ('-out:"' + $executable + '"')) + $sources + @('"' + $testSource + '"')
        Set-Content -LiteralPath $testResponse -Value $arguments -Encoding UTF8
        Write-Host "Compiling current runtime sources for $checkName..."
        # Pass the response-file token as a literal argument, not PowerShell splatting.
        $compilerArguments = @($compiler, ('@' + $testResponse))
        & $netCoreRunner @compilerArguments
        if ($LASTEXITCODE -ne 0) { throw "Compilation failed for $check (exit $LASTEXITCODE)." }
        & $mono $executable
        if ($LASTEXITCODE -ne 0) { throw "$check failed (exit $LASTEXITCODE)." }
    }
    Write-Host 'All creature, visitor and economy logic checks passed. Unity Play mode still needs a separate visual and interaction check.'
}
catch {
    [Console]::Error.WriteLine($_.Exception.Message)
    exit 1
}
finally {
    $env:MONO_PATH = $originalMonoPath
    if ($locationPushed) { Pop-Location }
}
