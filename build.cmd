@echo off
rem Builds a single self-contained exe (no .NET install needed) into the release folder.
rem config.json, token.json and lyrics_cache in the release folder are kept.
setlocal
cd /d "%~dp0"

rem files from older framework-dependent builds
del /q release\*.dll release\*.pdb release\spotify-lyrics-overlay.deps.json release\spotify-lyrics-overlay.runtimeconfig.json 2>nul

dotnet publish spotify-lyrics-overlay\spotify-lyrics-overlay.csproj -c Release -o release ^
    -r win-x64 --self-contained ^
    -p:PublishSingleFile=true ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    -p:EnableCompressionInSingleFile=true ^
    -p:DebugType=None
if errorlevel 1 (
    echo Build failed.
    exit /b 1
)

echo Release folder updated.
