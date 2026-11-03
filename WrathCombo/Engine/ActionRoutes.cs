using WrathCombo.Core;
using WrathCombo.Combos.PvE;
using WrathCombo.Services;
using WrathCombo.CustomComboNS.Functions;
using WrathFork.Contract;
using System.Linq;
using ECommons.DalamudServices;
using WrathCombo.Native;
using System.Collections.Generic;

namespace WrathCombo.Engine;

internal static class ActionRoutes
{
    private static readonly Dictionary<uint, ActionRoutePolicy> directInputs = new();
    internal static ActionRoutePolicy? For(uint input)
    {
        if (directInputs.TryGetValue(input, out var direct)) return direct;
        if (CustomComboFunctions.InPvP()) input = PvPAreaChoice.RouteInput(input, PvPIntentRouting.Area);
        var map = CustomComboFunctions.InPvP() ? Service.Configuration.PvPActionRoutes : Service.Configuration.ActionRoutes;
        if (!map.TryGetValue(Options.Job, out var routes)) return null;
        if (routes.TryGetValue(input, out var policy)) return policy;
        // Native upgrades may be supplied by the game/auto runner instead of the
        // assigned root. Use the native hook only, never recurse through ours.
        return routes.FirstOrDefault(r => r.Key < 1000000 && CustomComboFunctions.OriginalHook(r.Key) == input).Value;
    }
    internal static bool Allowed(uint action) => ActionRouteContext.Allowed(action) &&
        !ActionRouteContext.Excluded.Any(id => CustomComboFunctions.OriginalHook(id) == action);
    internal static uint Filter(uint action) => Allowed(action) ? action : All.Cease;

    internal static void RegisterDirectActions(CustomActionManager manager)
    {
        Service.Configuration.RegisteredDirectActions.UnionWith(Service.Configuration.ActionRoutes.Values.Concat(Service.Configuration.PvPActionRoutes.Values)
            .SelectMany(r => r.Values).Where(r => r.Direct && r.Target is > 0 and < 1000000).Select(r => r.Target));
        var registered = manager.Actions.Select(a => a.Id).ToHashSet();
        foreach (uint target in Service.Configuration.RegisteredDirectActions.Where(id => id is > 0 and < 1000000))
        {
            uint input = DirectAction.Id(target);
            directInputs[input] = new() { Direct = true, Target = target };
            if (!registered.Add(input)) continue;
            var row = Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.Action>().GetRowOrDefault(target);
            if (row is null) continue;
            manager.Register(new CustomAction(input, row.Value.Name.ToString(), "Use this specific action directly.", row.Value.Icon));
        }
    }
}
