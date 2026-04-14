@echo off
cd /d "%~dp0Assets\UniText"
del LICENSE.md
del LICENSE.md.meta
cd /d "%~dp0"
node assetstore-prepare.js
echo Done. Upload to Asset Store, then run: git checkout .
