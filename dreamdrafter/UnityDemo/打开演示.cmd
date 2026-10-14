@echo off
if exist "%~dp0Builds\Windows\DashedDemo.exe" (
    start "" "%~dp0Builds\Windows\DashedDemo.exe"
    exit /b 0
)
echo This repository contains the Unity source project, not a prebuilt player.
echo Open this folder in Unity Hub with Unity 6000.6.5f1.
echo Open Assets/Scenes/00_Workshop.unity, then press Play.
echo Project: %~dp0
pause
