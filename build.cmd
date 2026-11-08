@echo off
setlocal
set HERE=%~dp0
set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%HERE%dist" mkdir "%HERE%dist"

"%CSC%" /nologo /target:winexe /optimize+ /codepage:65001 /win32icon:"%HERE%assets\app.ico" ^
 /out:"%HERE%dist\GoHModManager.exe" ^
 /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll ^
 /reference:System.Windows.Forms.dll /reference:System.IO.Compression.dll ^
 /reference:System.IO.Compression.FileSystem.dll ^
 "%HERE%src\*.cs"
if errorlevel 1 (
  echo BUILD FAILED
  exit /b 1
)
echo BUILD OK
