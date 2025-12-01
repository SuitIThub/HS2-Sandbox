# Setting Up HS2_Managed Path

The plugin needs to reference Honey Select 2's game assemblies. You have several options to set the path:

## Option 1: Set in Project File (Recommended)

Edit `HS2SandboxPlugin.csproj` and change the default path in the `<HS2_Managed>` property:

```xml
<HS2_Managed Condition="'$(HS2_Managed)' == ''">C:\Path\To\Your\HS2_Data\Managed</HS2_Managed>
```

Replace `C:\Path\To\Your\HS2_Data\Managed` with your actual Honey Select 2 Managed folder path.

**Example:**
```xml
<HS2_Managed Condition="'$(HS2_Managed)' == ''">D:\Games\HoneySelect2\HS2_Data\Managed</HS2_Managed>
```

## Option 2: Set as Windows Environment Variable

1. Press `Win + R`, type `sysdm.cpl`, press Enter
2. Go to the **Advanced** tab
3. Click **Environment Variables**
4. Under **User variables** (or **System variables**), click **New**
5. Variable name: `HS2_Managed`
6. Variable value: `C:\Path\To\Your\HS2_Data\Managed` (your actual path)
7. Click **OK** on all dialogs
8. **Restart your IDE** for changes to take effect

## Option 3: Set in Visual Studio

1. Right-click the project in Solution Explorer
2. Select **Properties**
3. Go to **Build** tab
4. Click **Edit** next to "Conditional compilation symbols" or go to **Build Events**
5. Or set it in **Project Properties > Build > General > Pre-build event**:
   ```
   set HS2_Managed=C:\Path\To\Your\HS2_Data\Managed
   ```

## Option 4: Set in VS Code

Create or edit `.vscode/settings.json` in your project root:

```json
{
    "dotnet.defaultSolution": "HS2SandboxPlugin.sln",
    "terminal.integrated.env.windows": {
        "HS2_Managed": "C:\\Path\\To\\Your\\HS2_Data\\Managed"
    }
}
```

## Option 5: Set Temporarily in Command Line

For a single build session:

**Windows Command Prompt:**
```cmd
set HS2_Managed=C:\Path\To\Your\HS2_Data\Managed
dotnet build
```

**PowerShell:**
```powershell
$env:HS2_Managed = "C:\Path\To\Your\HS2_Data\Managed"
dotnet build
```

**WSL/Bash:**
```bash
export HS2_Managed="/mnt/c/Path/To/Your/HS2_Data/Managed"
dotnet build
```

## Finding Your HS2_Data Folder

The `HS2_Data` folder is typically located in your Honey Select 2 installation directory. Common locations:
- `C:\Games\HoneySelect2\HS2_Data\Managed`
- `D:\Illusion\HoneySelect2\HS2_Data\Managed`
- `Steam\steamapps\common\HoneySelect2\HS2_Data\Managed` (if installed via Steam)

Inside `HS2_Data`, you should see a `Managed` folder containing DLL files like `Assembly-CSharp.dll` and `Studio.dll`.

