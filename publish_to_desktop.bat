@echo off
setlocal

set DEST=%USERPROFILE%\Desktop\Galaxy Sky

echo Building One Who Stands Against The Horde...
dotnet publish -c Release -r win-x64 --self-contained true ^
    -p:PublishSingleFile=true ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    -o "%DEST%" 2>&1

if errorlevel 1 (
    echo.
    echo Build FAILED. Make sure .NET 8 SDK is installed:
    echo   https://dotnet.microsoft.com/download
    pause
    exit /b 1
)

REM Copy assets folder if it exists
if exist assets (
    xcopy /E /I /Y assets "%DEST%\assets" >nul
    echo Copied assets folder.
)

echo.
echo Done! Game published to:
echo   %DEST%\OWSATH.exe
echo.
pause
