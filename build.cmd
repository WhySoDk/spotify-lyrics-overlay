@echo off
rem Builds the app in Release mode and copies it into the release folder.
rem config.json, token.json and lyrics_cache in the release folder are kept.
setlocal
cd /d "%~dp0"

dotnet publish spotify-lyrics-overlay\spotify-lyrics-overlay.csproj -c Release -o release --no-self-contained -p:DebugType=None
if errorlevel 1 (
    echo Build failed.
    exit /b 1
)

echo Release folder updated.
