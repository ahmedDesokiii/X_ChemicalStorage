@echo off
rem BioBank lab install. Right-click > Run as administrator.
net session >nul 2>&1
if %errorlevel% neq 0 (
  echo.
  echo  Right-click INSTALL.cmd and choose "Run as administrator".
  echo.
  pause
  exit /b 1
)
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install-all.ps1"
echo.
pause
