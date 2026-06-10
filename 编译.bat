@echo off
chcp 65001 >nul
setlocal
rem == 一份源码 吾爱熄屏.cs 编译出三个版本(系统自带 .NET Framework 4 的 csc,无需装环境) ==
set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" ( echo [X] 找不到 csc.exe，请确认已装 .NET Framework 4（Win10/11 自带）。& pause & exit /b 1 )
set "REFS=/reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Management.dll"
set "OPT=/nologo /target:winexe /optimize+ /codepage:65001"

echo 正在编译三个版本 ...
"%CSC%" %OPT% /win32icon:app.ico        /define:LOCK %REFS% /out:"吾爱熄屏.exe"       "吾爱熄屏.cs"
"%CSC%" %OPT% /win32icon:app_blue.ico   /define:PWD  %REFS% /out:"吾爱熄屏口令版.exe"  "吾爱熄屏.cs"
"%CSC%" %OPT% /win32icon:app_orange.ico              %REFS% /out:"吾爱熄屏融合版.exe"  "吾爱熄屏.cs"

echo.
if exist "吾爱熄屏融合版.exe" ( echo [OK] 完成:吾爱熄屏.exe / 吾爱熄屏口令版.exe / 吾爱熄屏融合版.exe ) else ( echo [X] 编译失败,请看上面报错。 )
pause
