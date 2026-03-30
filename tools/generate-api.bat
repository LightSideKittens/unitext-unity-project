@echo off
cd /d "%~dp0UniTextDocGen"
dotnet run -- "../../Assets/UniText" "../api.json"
pause