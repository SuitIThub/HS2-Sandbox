# Build setup

The solution uses **NuGet** only. `nuget.config` registers:

- **nuget.org** — general packages (e.g. `UnityEngine.Modules`)
- **BepInEx** — `https://nuget.bepinex.dev/v3/index.json`
- **IllusionLibs** (IllusionMods) — `Assembly-CSharp`, Unity UI, `IllusionModdingAPI.HS2API`, and related game assemblies for Honey Select 2

You do **not** need to point at a local `HS2_Data/Managed` folder; that was the older workflow before IllusionLibs `PackageReference` entries in `Directory.Build.props`.

If Visual Studio does not list IllusionLibs packages, close and reopen the solution after pulling `nuget.config`, or run `dotnet restore` from the repo root.
