@echo off
setlocal

cd /d "%~dp0Packages\media.lightside.unitext"

set BUILD_DIR=%~dp0Builds\Package
if not exist "%BUILD_DIR%" mkdir "%BUILD_DIR%"

call node "tools~/samples-pack.js" hide .
if errorlevel 1 exit /b 1

call npm pack
set PACK_EXIT=%errorlevel%

call node "tools~/samples-pack.js" show .

if %PACK_EXIT% neq 0 (
    echo ERROR: npm pack failed
    exit /b 1
)

for %%f in (media.lightside.unitext-*.tgz) do (
    move /y "%%f" "%BUILD_DIR%\"
    echo Packed: Builds\Package\%%f
)

for %%f in ("%BUILD_DIR%\media.lightside.unitext-*.tgz") do (
    echo Size: %%~zf bytes
    tar -tzf "%%f" 2>nul | find /c /v ""
)
