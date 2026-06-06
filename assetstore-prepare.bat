@echo off
cd /d "%~dp0Assets\UniText"
del LICENSE.md
del LICENSE.md.meta
cd /d "%~dp0"
move /Y "%~dp0Assets\UniText\Defaults\UniTextSettings.asset" "%~dp0Assets\UniText\Resources\UniTextSettings.asset"
del "%~dp0Assets\UniText\Defaults\UniTextSettings.asset.meta"
node assetstore-prepare.js
echo Done. Upload to Asset Store, then run: git checkout .
