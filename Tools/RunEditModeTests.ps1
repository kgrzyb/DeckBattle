param(
    [string]$UnityPath = "D:\Unity\2022.3.62f3\Editor\Unity.exe"
)

$ErrorActionPreference = "Stop"

$projectPath = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$projectPathForward = $projectPath.Replace("\", "/")
$testResultsPath = Join-Path $projectPath "TestResults_EditMode.xml"
$logPath = Join-Path $projectPath "Logs\EditModeTests.log"

try {
    $openProjectEditors = Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" |
        Where-Object {
            $_.CommandLine -and
            ($_.CommandLine.Contains($projectPath) -or $_.CommandLine.Contains($projectPathForward))
        }
}
catch {
    $openUnityEditors = Get-Process Unity -ErrorAction SilentlyContinue
    if ($openUnityEditors) {
        Write-Host "Could not inspect Unity process command lines, and a Unity Editor process is running. Close Unity Editor or run EditMode tests from the existing Editor with menu: DeckBattle > Tests > Run EditMode Tests."
        exit 2
    }

    $openProjectEditors = @()
}

if ($openProjectEditors) {
    Write-Host "DeckBattle is already open in Unity Editor. Run EditMode tests from the existing Editor with menu: DeckBattle > Tests > Run EditMode Tests. Close the Editor before using this CLI script."
    exit 2
}

if (-not (Test-Path $UnityPath)) {
    Write-Host "Unity executable was not found: $UnityPath"
    exit 3
}

& $UnityPath `
    -batchmode `
    -projectPath $projectPath `
    -runTests `
    -testPlatform EditMode `
    -testResults $testResultsPath `
    -logFile $logPath `
    -quit

exit $LASTEXITCODE
