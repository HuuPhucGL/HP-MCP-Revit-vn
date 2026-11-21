@echo off
rem Go add-in HP MCP Revit va bo dang ky MCP
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0installer\install.ps1" -GoCai
pause
