using System;
using System.Collections.Generic;

namespace HS2SandboxPlugin
{
    /// <summary>Resolved param payload for the current subtimeline run (set on <see cref="TimelineContext"/> before subcommands execute).</summary>
    public sealed class SubTimelineParamRuntime
    {
        public string VariableName = "";
        public SubTimelineParamKind Kind;
        public string StringValue = "";
        public int IntValue;
        public bool BoolValue;
        public List<string> ListItems = new List<string>();
        public Dictionary<string, string> DictItems = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public void ApplyTo(TimelineVariableStore store)
        {
            string name = (VariableName ?? "").Trim();
            if (string.IsNullOrEmpty(name)) return;
            switch (Kind)
            {
                case SubTimelineParamKind.String:
                    store.SetStringExclusive(name, StringValue ?? "");
                    break;
                case SubTimelineParamKind.Int:
                    store.SetIntExclusive(name, IntValue);
                    break;
                case SubTimelineParamKind.Bool:
                    store.SetBoolExclusive(name, BoolValue);
                    break;
                case SubTimelineParamKind.List:
                    store.SetListExclusive(name, ListItems);
                    break;
                case SubTimelineParamKind.Dict:
                    store.SetDictExclusive(name, DictItems);
                    break;
            }
        }

        /// <summary>Builds runtime values from the parent row editor state (no variable interpolation).</summary>
        public static SubTimelineParamRuntime? FromCommand(SubTimelineParamCommand? cmd, SubTimelineParamInputs inputs)
        {
            if (cmd == null || string.IsNullOrWhiteSpace(cmd.VariableName)) return null;
            string name = cmd.VariableName.Trim();
            var r = new SubTimelineParamRuntime { VariableName = name, Kind = cmd.Kind };
            switch (cmd.Kind)
            {
                case SubTimelineParamKind.String:
                    r.StringValue = inputs.StringText ?? "";
                    break;
                case SubTimelineParamKind.Int:
                    r.IntValue = int.TryParse((inputs.IntText ?? "").Trim(), out int iv) ? iv : 0;
                    break;
                case SubTimelineParamKind.Bool:
                    r.BoolValue = inputs.BoolValue;
                    break;
                case SubTimelineParamKind.List:
                    r.ListItems = new List<string>(inputs.ListItems);
                    break;
                case SubTimelineParamKind.Dict:
                    r.DictItems = new Dictionary<string, string>(inputs.Dict, StringComparer.OrdinalIgnoreCase);
                    break;
            }
            return r;
        }
    }
}
