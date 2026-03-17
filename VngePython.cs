using System;
using System.Linq;
using System.Reflection;
using Microsoft.Scripting.Hosting;

public static class VngePython
{
    private static ScriptEngine? _engine;

    public static ScriptEngine GetEngine()
    {
        if (_engine != null) return _engine;

        // Assembly finden (Name kann "Unity.Console" sein)
        var asm = AppDomain.CurrentDomain
            .GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name.Equals("Unity.Console", StringComparison.OrdinalIgnoreCase));

        if (asm == null)
            throw new Exception("Unity.Console.dll nicht geladen. Ist das Console-Plugin aktiv?");

        var programType = asm.GetType("Unity.Console.Program", throwOnError: true);

        // MainEngine ist internal static ScriptEngine { get; private set; }
        var prop = programType.GetProperty("MainEngine",
            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

        if (prop == null)
            throw new Exception("Unity.Console.Program.MainEngine nicht gefunden (Reflection).");

        var engine = prop.GetValue(null, null) as ScriptEngine;

        if (engine == null)
            throw new Exception("MainEngine ist null (Console noch nicht initialisiert?).");

        _engine = engine;
        return engine;
    }

    static string PyQuote(string s) => s.Replace("\\", "\\\\").Replace("'", "\\'");

    static void ExecSSS(string pyCallLine)
    {
        var eng = GetEngine(); // dein bestehender Getter für Unity.Console.Program.MainEngine

        // init falls nötig + call
        var code = $@"
import scenesavestate
from vngameengine import vnge_game
if scenesavestate._sc is None:
    scenesavestate.autorun_start(vnge_game)

{pyCallLine}
";
        eng.Execute(code);
    }

    // dann:
    public static void GotoNext()     => ExecSSS("scenesavestate._sc.goto_next()");
    public static void GotoPrev()     => ExecSSS("scenesavestate._sc.goto_prev()");
    public static void GotoNextSc()   => ExecSSS("scenesavestate._sc.goto_next_sc()");
    public static void GotoPrevSc(bool lastCam)
        => ExecSSS($"scenesavestate._sc.goto_prev_sc(lastcam={(lastCam ? "True" : "False")})");

    /// <summary>Scene navigation: next (within scene).</summary>
    public static void SceneNext() => GotoNext();
    /// <summary>Scene navigation: previous (within scene).</summary>
    public static void ScenePrev() => GotoPrev();
    /// <summary>Scene navigation: next scene.</summary>
    public static void NextScene() => GotoNextSc();
    /// <summary>Scene navigation: previous scene (lastCam = false).</summary>
    public static void PrevScene() => GotoPrevSc(false);

    /// <summary>Load scene by index. Calls SceneConsole.loadSceneByIndex(index).</summary>
    public static void LoadSceneByIndex(int index) => ExecSSS($"scenesavestate._sc.loadSceneByIndex({index})");

    /// <summary>Get maximum valid scene index from vnge.</summary>
    public static int GetMaxSceneIndex()
    {
        var eng = GetEngine();
        var code = @"
import scenesavestate
from vngameengine import vnge_game
if scenesavestate._sc is None:
    scenesavestate.autorun_start(vnge_game)
scenesavestate._sc.getMaxSceneIndex()
";
        return eng.Execute<int>(code);
    }

    public static void Exec(string code)
    {
        var eng = GetEngine();
        eng.Execute(code);
    }
}