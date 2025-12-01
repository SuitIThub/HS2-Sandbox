# Building the Plugin

## Prerequisites

- .NET SDK 6.0 or later (download from [dotnet.microsoft.com](https://dotnet.microsoft.com/download))
- Honey Select 2 Managed folder path set in `HS2SandboxPlugin.csproj` (already done!)

## Build Methods

### Method 1: Using Visual Studio (Easiest)

1. Open `HS2-Sandbox.sln` in Visual Studio
2. Select **Build > Build Solution** (or press `Ctrl+Shift+B`)
3. The DLL will be in `bin\Debug\HS2SandboxPlugin.dll` (or `bin\Release\` for Release builds)

### Method 2: Using .NET CLI (Command Line)

Open a terminal in the project directory and run:

**Debug build:**
```bash
dotnet build
```

**Release build:**
```bash
dotnet build -c Release
```

The DLL will be in:
- Debug: `bin\Debug\HS2SandboxPlugin.dll`
- Release: `bin\Release\HS2SandboxPlugin.dll`

### Method 3: Using the Build Script

**Windows (PowerShell):**
```powershell
.\build.ps1
```

**Windows (Command Prompt):**
```cmd
build.bat
```

## After Building

1. Copy `HS2SandboxPlugin.dll` from `bin\Debug\` (or `bin\Release\`) to:
   ```
   [Your HS2 Install]\BepInEx\plugins\
   ```

2. Launch StudioNeoV2 and look for the "Sandbox" button in the sidebar!

## Troubleshooting

**Error: "Could not find file 'Studio.dll'"**
- Verify the path in `HS2SandboxPlugin.csproj` line 12 is correct
- Make sure the Managed folder contains `Studio.dll`, `Assembly-CSharp.dll`, etc.

**Error: "The type or namespace name 'Studio' could not be found"**
- The build should still work even if the IDE shows this error
- Try building from command line: `dotnet build`

**Build succeeds but plugin doesn't load in game**
- Make sure BepInEx 5.4.21+ is installed
- Check `BepInEx\LogOutput.log` for errors
- Verify the DLL is in `BepInEx\plugins\` (not `BepInEx\plugins\HS2SandboxPlugin\`)

