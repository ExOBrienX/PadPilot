<#
  Crea PadPilot.msix para subirlo a la Microsoft Store (la Store lo firma; no hace falta certificado).

  Requisitos: .NET 10 SDK y Windows SDK (makeappx.exe y makepri.exe):
    winget install Microsoft.WindowsSDK.10.0.26100

  Uso (los tres datos salen de Partner Center → tu app → Product management → Product identity):
    .\store\microsoft\build-msix.ps1 -IdentityName "12345ExO.PadPilot" `
        -Publisher "CN=XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX" -PublisherDisplayName "ExO" -Version 1.0.0.0
#>
param(
    [Parameter(Mandatory)] [string] $IdentityName,
    [Parameter(Mandatory)] [string] $Publisher,
    [Parameter(Mandatory)] [string] $PublisherDisplayName,
    [string] $Version = "1.0.0.0"
)
$ErrorActionPreference = "Stop"

$here = $PSScriptRoot
$root = Resolve-Path "$here\..\.."
$build = Join-Path $here "build"
$layout = Join-Path $build "layout"

function Find-SdkTool($name) {
    $tool = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\$name" -ErrorAction SilentlyContinue |
        Sort-Object FullName -Descending | Select-Object -First 1
    if (-not $tool) { throw "No se encontró $name. Instala el Windows SDK: winget install Microsoft.WindowsSDK.10.0.26100" }
    $tool.FullName
}
$makeappx = Find-SdkTool "makeappx.exe"
$makepri = Find-SdkTool "makepri.exe"

if ($Version -notmatch '^\d+\.\d+\.\d+\.0$') { throw "La versión debe tener 4 partes y terminar en .0 (p. ej. 1.0.0.0): la Store lo exige." }

# 1) Compilar la app en la carpeta del paquete
Remove-Item $build -Recurse -Force -ErrorAction SilentlyContinue
$appVersion = $Version -replace '\.0$', ''
dotnet publish "$root\src\PadPilot.App" -c Release -r win-x64 --self-contained `
    -p:PublishReadyToRun=true -p:DebugType=none -p:Version=$appVersion -o "$layout\PadPilot"
if ($LASTEXITCODE -ne 0) { throw "dotnet publish falló" }
Copy-Item "$root\LICENSE", "$root\THIRD-PARTY-NOTICES.txt" "$layout\PadPilot\"

# 2) Manifiesto con tus datos y logos
(Get-Content "$here\AppxManifest.xml" -Raw) `
    -replace '__IDENTITY_NAME__', $IdentityName `
    -replace '__PUBLISHER__', [System.Security.SecurityElement]::Escape($Publisher) `
    -replace '__PUBLISHER_DISPLAY_NAME__', [System.Security.SecurityElement]::Escape($PublisherDisplayName) `
    -replace '__VERSION__', $Version |
    Set-Content "$layout\AppxManifest.xml" -Encoding utf8
Copy-Item "$here\Assets" "$layout\Assets" -Recurse

# 3) Índice de recursos (para que Windows elija el logo según la escala de pantalla)
Push-Location $build
& $makepri createconfig /cf priconfig.xml /dq en-US /o | Out-Null
& $makepri new /pr $layout /cf priconfig.xml /of "$layout\resources.pri" /o
if ($LASTEXITCODE -ne 0) { Pop-Location; throw "makepri falló" }
Pop-Location

# 4) Empaquetar
$msix = Join-Path $build "PadPilot-$Version.msix"
& $makeappx pack /d $layout /p $msix /o
if ($LASTEXITCODE -ne 0) { throw "makeappx falló" }

Write-Host ""
Write-Host "Listo: $msix" -ForegroundColor Green
Write-Host "Súbelo en Partner Center → tu app → Packages."
