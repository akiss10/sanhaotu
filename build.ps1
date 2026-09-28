$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$framework = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319"
$csc = Join-Path $framework "csc.exe"
$source = Join-Path $root "Program.cs"
$icon = Join-Path $root "assets\app.ico"
$spritesheet = Join-Path $root "assets\spritesheet.png"
$outputDirectory = Join-Path $root "dist"
$output = Join-Path $outputDirectory "SanhaoTuDesktopPet.exe"
$resourceName = "SanhaoTuDesktopPet.assets.spritesheet.png"

function Find-GacAssembly([string]$name) {
    $assemblyRoot = Join-Path $env:WINDIR "Microsoft.NET\assembly"
    $directories = Get-ChildItem $assemblyRoot -Directory
    foreach ($architecture in $directories) {
        $assemblyDirectory = Join-Path $architecture.FullName $name
        if (Test-Path -LiteralPath $assemblyDirectory) {
            $file = Get-ChildItem $assemblyDirectory -Recurse -Filter "$name.dll" |
                Select-Object -First 1
            if ($null -ne $file) {
                return $file.FullName
            }
        }
    }
    throw "Unable to locate $name.dll in the .NET Framework GAC."
}

if (-not (Test-Path -LiteralPath $csc)) {
    throw "The .NET Framework C# compiler was not found at $csc"
}
if (-not (Test-Path -LiteralPath $spritesheet)) {
    throw "The converted spritesheet is missing: $spritesheet"
}
if (-not (Test-Path -LiteralPath $icon)) {
    throw "The application icon is missing: $icon"
}

New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null

$references = @(
    (Join-Path $framework "System.dll"),
    (Join-Path $framework "System.Core.dll"),
    (Find-GacAssembly "PresentationFramework"),
    (Find-GacAssembly "PresentationCore"),
    (Find-GacAssembly "WindowsBase"),
    (Find-GacAssembly "System.Xaml")
)

$arguments = @(
    "/nologo",
    "/target:winexe",
    "/platform:x64",
    "/optimize+",
    "/debug-",
    "/utf8output",
    ("/out:" + $output),
    ("/win32icon:" + $icon),
    ("/resource:" + $spritesheet + "," + $resourceName)
)

foreach ($reference in $references) {
    $arguments += ("/reference:" + $reference)
}
$arguments += $source

& $csc $arguments
if ($LASTEXITCODE -ne 0) {
    throw "Compilation failed with exit code $LASTEXITCODE"
}

$localizedOutput = Join-Path $outputDirectory "三好兔桌面宠物.exe"
try {
    Copy-Item -Force -LiteralPath $output -Destination $localizedOutput
} catch [System.IO.IOException] {
    $localizedOutput = Join-Path $outputDirectory "三好兔桌面宠物-新版.exe"
    Copy-Item -Force -LiteralPath $output -Destination $localizedOutput
    Write-Warning "The original localized EXE is currently running; wrote the updated build to $localizedOutput instead."
}

$item = Get-Item -LiteralPath $output
$localizedItem = Get-Item -LiteralPath $localizedOutput
Write-Host ("Built: " + $item.FullName)
Write-Host ("Localized: " + $localizedItem.FullName)
Write-Host ("Size:  " + $item.Length + " bytes")