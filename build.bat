@echo off
rem Builds WindowsCenter.exe using the C# compiler that ships with Windows (.NET Framework 4.x).
rem No SDK or extra installs needed.
setlocal
cd /d "%~dp0"

set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
  echo Could not find csc.exe. .NET Framework 4.x is required.
  exit /b 1
)

"%CSC%" /nologo /target:winexe /optimize+ /out:WindowsCenter.exe ^
  /r:System.Windows.Forms.dll /r:System.Drawing.dll ^
  src\WindowsCenter.cs
if errorlevel 1 exit /b 1

echo Built WindowsCenter.exe
