# ============================================================
#  Corkboard - build / publish script
#  Usage:
#    powershell -ExecutionPolicy Bypass -File setup.ps1                            # build Debug
#    powershell -ExecutionPolicy Bypass -File setup.ps1 -Publish                   # framework-dependent single-file (~3 MB, needs .NET 8 Desktop Runtime)
#    powershell -ExecutionPolicy Bypass -File setup.ps1 -Publish -SelfContained    # self-contained single-file (~150 MB, no runtime needed)
# ============================================================
param(
    [switch]$Publish,
    [switch]$SelfContained
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$proj = Join-Path $root "Corkboard.csproj"
$sln  = Join-Path $root "Corkboard.sln"

Write-Host "== Check .NET SDK ==" -ForegroundColor Cyan
$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) {
    Write-Host "dotnet not found. Install .NET 8 SDK first:" -ForegroundColor Red
    Write-Host "  https://dotnet.microsoft.com/download/dotnet/8.0"
    exit 1
}
Write-Host "dotnet: $(& dotnet --version)"

if ($Publish) {
    $out = Join-Path $root "publish"
    if ($SelfContained) {
        Write-Host "`n== Publish self-contained single-file (portable, needs internet first time) ==" -ForegroundColor Cyan
        & dotnet publish $proj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o $out
    }
    else {
        Write-Host "`n== Publish single-file (framework-dependent, needs .NET 8 Desktop Runtime) ==" -ForegroundColor Cyan
        & dotnet publish $proj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o $out
    }
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    Write-Host "`nDone:" -ForegroundColor Green
    Write-Host "  $out\Corkboard.exe" -ForegroundColor Green
    Write-Host "  Copy the whole publish folder (or just the exe) to run; data is stored next to the exe."
}
else {
    Write-Host "`n== Build (Debug) ==" -ForegroundColor Cyan
    & dotnet build $sln -c Debug
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    Write-Host "`nDone:" -ForegroundColor Green
    Write-Host "  $root\bin\Debug\net8.0-windows\Corkboard.exe" -ForegroundColor Green
}
