@echo off
set UNITEXT=%~dp0Packages\media.lightside.unitext
set CORE=%~dp0Packages\media.lightside.core
set MYSPACE=%~dp0Assets\UniText_MySpace
set METASTASH=%TEMP%\lightside-assetstore-meta
call node "%UNITEXT%\tools~\samples-pack.js" hide "%UNITEXT%"
if errorlevel 1 exit /b 1
if not exist "%METASTASH%" mkdir "%METASTASH%"
if exist "%MYSPACE%\WebGLDemo" move /Y "%MYSPACE%\WebGLDemo" "%MYSPACE%\WebGLDemo~"
if exist "%MYSPACE%\WebGLDemo.meta" move /Y "%MYSPACE%\WebGLDemo.meta" "%METASTASH%\WebGLDemo.meta"
if exist "%MYSPACE%\Slideshow" move /Y "%MYSPACE%\Slideshow" "%MYSPACE%\Slideshow~"
if exist "%MYSPACE%\Slideshow.meta" move /Y "%MYSPACE%\Slideshow.meta" "%METASTASH%\Slideshow.meta"
del "%UNITEXT%\LICENSE.md"
del "%UNITEXT%\LICENSE.md.meta"
move /Y "%CORE%\LICENSE.md" "%CORE%\LICENSE-LightSide.Core.md"
del "%CORE%\LICENSE.md.meta"
node assetstore-prepare.js
echo Done. Upload to Asset Store, then run: assetstore-restore.bat
