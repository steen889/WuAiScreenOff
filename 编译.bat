@echo off
chcp 65001 >nul
setlocal
rem == 吾爱熄屏 v2.0 一键编译 ==
rem  前三个由同一份 吾爱熄屏.cs 用 /define 编出;锁屏、休眠为独立单文件。
rem  系统自带 .NET Framework 4 的 csc,无需装环境;无需 System.Management。
set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" ( echo [X] 找不到 csc.exe，请确认已装 .NET Framework 4（Win7-Win11 自带）。& pause & exit /b 1 )
set "REFS=/reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll"
set "OPT=/nologo /target:winexe /optimize+ /codepage:65001"

echo 正在编译 ...
"%CSC%" %OPT% /win32icon:app.ico        /define:LOCK %REFS% /out:"吾爱熄屏.exe"       "吾爱熄屏.cs"
"%CSC%" %OPT% /win32icon:app_orange.ico              %REFS% /out:"吾爱熄屏融合版.exe"  "吾爱熄屏.cs"
rem -- 口令版 DPMS 两版对照(只差关屏级别,用户实机对比后收敛为一版;见 HANDOFF.md ④) --
"%CSC%" %OPT% /win32icon:app_purple.ico /define:PWD;PWD_NODPMS %REFS% /out:"吾爱熄屏口令版A-纯黑窗不关屏.exe" "吾爱熄屏.cs"
"%CSC%" %OPT% /win32icon:app_blue.ico   /define:PWD            %REFS% /out:"吾爱熄屏口令版B-现状DPMS全关.exe" "吾爱熄屏.cs"
rem -- 锁屏 / 休眠:独立单文件,均极简(原生动作+立即DPMS关屏) --
"%CSC%" %OPT% /win32icon:app_red.ico /reference:System.dll /out:"吾爱熄屏-一键锁屏熄屏.exe" "一键锁屏熄屏.cs"
"%CSC%" %OPT% /win32icon:app.ico /reference:System.dll /reference:System.Windows.Forms.dll /out:"吾爱熄屏-一键休眠黑屏.exe" "一键休眠黑屏.cs"

echo.
if exist "吾爱熄屏融合版.exe" ( echo [OK] 完成: 吾爱熄屏 / 融合版 / 口令版A·B / 一键锁屏熄屏 / 一键休眠黑屏 ) else ( echo [X] 编译失败,请看上面报错。 )
pause
