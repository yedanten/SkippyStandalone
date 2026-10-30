<#
.SYNOPSIS
    构建 Skippy 独立版：编译原生组件、发布单文件 EXE，并可选地整理 Release 目录。

.DESCRIPTION
    1. 使用 Visual Studio 自带的 CMake 构建 Skippy.Native.dll（x64 / Release）。
    2. 把原生 DLL 作为嵌入资源复制到 StandaloneApp。
    3. dotnet publish 生成 win-x64、self-contained、single-file 的 StandaloneApp.exe。
    4. 输出到 Release/StandaloneApp.exe。

.PARAMETER SkipNative
    跳过原生组件编译（复用上次产物）。

.PARAMETER Configuration
    .NET 配置，默认 Release。

.EXAMPLE
    pwsh -File tools/build.ps1
    pwsh -File tools/build.ps1 -SkipNative
#>
param(
    [switch]$SkipNative,
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$nativeProject = Join-Path $repoRoot "src\Skippy.Native"
$appProject = Join-Path $repoRoot "src\StandaloneApp\StandaloneApp.csproj"
$nativeBuildDir = Join-Path $repoRoot "artifacts\native-build"
$publishDir = Join-Path $repoRoot "artifacts\publish"
$releaseDir = Join-Path $repoRoot "Release"
$nativeAssetDir = Join-Path $repoRoot "src\StandaloneApp\Assets\native"

function Find-CMake {
    $vswhere = "C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path $vswhere) {
        $install = & $vswhere -latest -products * -property installationPath 2>$null
        if ($install) {
            $candidate = Join-Path $install "Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe"
            if (Test-Path $candidate) { return $candidate }
        }
    }

    $command = Get-Command cmake.exe -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }

    throw "未找到 CMake。请安装 Visual Studio 的 C++ 工作负载（含 CMake 组件）。"
}

Write-Host "==> 仓库根目录: $repoRoot"

if (-not $SkipNative) {
    $cmake = Find-CMake
    Write-Host "==> 使用 CMake: $cmake"

    New-Item -ItemType Directory -Force $nativeBuildDir | Out-Null

    & $cmake -S $nativeProject -B $nativeBuildDir -G "Visual Studio 18 2026" -A x64
    if ($LASTEXITCODE -ne 0) { throw "CMake 配置失败。" }

    & $cmake --build $nativeBuildDir --config Release --parallel
    if ($LASTEXITCODE -ne 0) { throw "原生组件编译失败。" }

    $nativeDll = Join-Path $nativeBuildDir "Release\Skippy.Native.dll"
    if (-not (Test-Path $nativeDll)) { throw "未找到编译产物: $nativeDll" }

    New-Item -ItemType Directory -Force $nativeAssetDir | Out-Null
    Copy-Item $nativeDll (Join-Path $nativeAssetDir "Skippy.Native.dll") -Force
    Write-Host "==> 已复制原生组件到 Assets/native"
}
else {
    Write-Host "==> 跳过原生组件编译"
}

Write-Host "==> 发布单文件 EXE ..."
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }

dotnet publish $appProject `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=none `
    -p:DebugSymbols=false `
    -o $publishDir

if ($LASTEXITCODE -ne 0) { throw "dotnet publish 失败。" }

$exe = Join-Path $publishDir "StandaloneApp.exe"
if (-not (Test-Path $exe)) { throw "未生成 StandaloneApp.exe。" }

New-Item -ItemType Directory -Force $releaseDir | Out-Null
Copy-Item $exe $releaseDir -Force

$size = [Math]::Round((Get-Item $exe).Length / 1MB, 2)
Write-Host "==> 完成: $releaseDir\StandaloneApp.exe ($size MB)"
