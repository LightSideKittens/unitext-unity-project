@echo off
setlocal

cd /d "%~dp0Assets\UniText"

for /f "delims=" %%b in ('git rev-parse --abbrev-ref HEAD') do set VERSION=%%b

node -e "const fs=require('fs');const p=JSON.parse(fs.readFileSync('package.json','utf8'));p.version='%VERSION%';fs.writeFileSync('package.json',JSON.stringify(p,null,4)+'\n')"

set BUILD_DIR=%~dp0Builds\Package
if not exist "%BUILD_DIR%" mkdir "%BUILD_DIR%"

if exist "Samples" (
    ren Samples Samples~
) else (
    echo ERROR: Samples directory not found
    exit /b 1
)

call npm pack
set PACK_EXIT=%errorlevel%

ren Samples~ Samples

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
