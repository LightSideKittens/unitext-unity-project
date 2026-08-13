@echo off
set UNITEXT=%~dp0Packages\media.lightside.unitext
set CORE=%~dp0Packages\media.lightside.core
set MYSPACE=%~dp0Assets\UniText_MySpace
set METASTASH=%TEMP%\lightside-assetstore-meta
call node "%UNITEXT%\tools~\samples-pack.js" show "%UNITEXT%"
if exist "%MYSPACE%\WebGLDemo~" move /Y "%MYSPACE%\WebGLDemo~" "%MYSPACE%\WebGLDemo"
if exist "%METASTASH%\WebGLDemo.meta" move /Y "%METASTASH%\WebGLDemo.meta" "%MYSPACE%\WebGLDemo.meta"
if exist "%MYSPACE%\Slideshow~" move /Y "%MYSPACE%\Slideshow~" "%MYSPACE%\Slideshow"
if exist "%METASTASH%\Slideshow.meta" move /Y "%METASTASH%\Slideshow.meta" "%MYSPACE%\Slideshow.meta"
del "%CORE%\LICENSE-LightSide.Core.md" 2>nul
del "%CORE%\LICENSE-LightSide.Core.md.meta" 2>nul
git -C "%UNITEXT%" checkout .
git -C "%CORE%" checkout .
git -C "%UNITEXT%" status --short
git -C "%CORE%" status --short
echo Restored. Both submodules must be clean above.
