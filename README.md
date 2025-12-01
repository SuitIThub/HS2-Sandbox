# HS2 Sandbox Plugin

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

1. Set the `HS2_Managed` environment variable to point to your Honey Select 2 `HS2_Data/Managed` folder
2. Build the project using Visual Studio or `dotnet build`

## Usage

1. Launch StudioNeoV2
2. Look for the sandbox button in the sidebar
3. Click it to open the main control window
4. Use checkboxes to toggle subwindows on/off

