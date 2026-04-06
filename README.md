# HS2 Sandbox Plugin

<!-- Version badge URLs point at this repo’s raw `versions.json`; update `SuitIThub/HS2-Sandbox` if you fork. -->

[![All-in-one](https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fraw.githubusercontent.com%2FSuitIThub%2FHS2-Sandbox%2Fmain%2Fversions.json&label=All-in-one&query=%24.allInOne&style=flat-square&color=0366d6)](https://github.com/SuitIThub/HS2-Sandbox/blob/main/HS2SandboxPlugin.cs)
[![CopyScript](https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fraw.githubusercontent.com%2FSuitIThub%2FHS2-Sandbox%2Fmain%2Fversions.json&label=CopyScript&query=%24.copyScript&style=flat-square&color=2ea043)](https://github.com/SuitIThub/HS2-Sandbox/blob/main/Modules/CopyScript/CopyScriptModulePlugin.cs)
[![Timeline](https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fraw.githubusercontent.com%2FSuitIThub%2FHS2-Sandbox%2Fmain%2Fversions.json&label=Timeline&query=%24.timeline&style=flat-square&color=8957e5)](https://github.com/SuitIThub/HS2-Sandbox/blob/main/Modules/Timeline/TimelineModulePlugin.cs)
[![SearchBarManager](https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fraw.githubusercontent.com%2FSuitIThub%2FHS2-Sandbox%2Fmain%2Fversions.json&label=SearchBarManager&query=%24.searchBarManager&style=flat-square&color=bc4c00)](https://github.com/SuitIThub/HS2-Sandbox/blob/main/Modules/SearchBarManager/SearchBarManagerModulePlugin.cs)

*Version badges read [`versions.json`](versions.json); CI regenerates it when `PluginVersion` constants change.*

A BepInEx plugin for Honey Select 2 that adds a sandbox interface in StudioNeoV2.

## Features

- Sidebar button in StudioNeoV2
- Main control window with checkboxes
- Toggleable subwindows (functionality to be added)

## Installation

1. Install [BepInEx 5.4.21](https://github.com/BepInEx/BepInEx/releases) for Honey Select 2
2. Place `HS2SandboxPlugin.dll` in `BepInEx/plugins/`
3. Launch StudioNeoV2

## Building

Game assemblies are restored from the [IllusionLibs](https://github.com/IllusionMods/IllusionLibs) NuGet feed (see `nuget.config`). Build with Visual Studio or `dotnet build HS2-Sandbox.sln -c Release`.

## Usage

1. Launch StudioNeoV2
2. Look for the sandbox button in the sidebar
3. Click it to open the main control window
4. Use checkboxes to toggle subwindows on/off

