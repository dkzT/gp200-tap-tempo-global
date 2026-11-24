@echo off
rem Compila o TapGlobalPatcher.exe com o compilador C# que ja vem no Windows (.NET Framework 4.x).
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%~dp0..\dist" mkdir "%~dp0..\dist"
"%CSC%" /nologo /optimize+ /codepage:65001 /target:winexe /win32manifest:"%~dp0app.manifest" /win32icon:"%~dp0icon.ico" /out:"%~dp0..\dist\TapGlobalPatcher.exe" "%~dp0Program.cs" "%~dp0Patcher.cs" "%~dp0MainForm.cs" "%~dp0AssemblyInfo.cs"
if errorlevel 1 (echo Falha na compilacao & exit /b 1)
echo OK: dist\TapGlobalPatcher.exe
