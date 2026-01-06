@echo off
cd /d "%~dp0UniTextDocGen"
"%USERPROFILE%\.dotnet\dotnet.exe" run -- "../../Assets/UniText" "../api.json"
pause