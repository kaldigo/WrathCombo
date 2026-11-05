using System;
using System.Collections.Generic;
using System.Linq;

namespace WrathCombo.Engine.Selection;

internal static class TargetOrder
{
    internal static readonly string[] AllySources = ["PartyMouseover", "WorldMouseover", "Selected", "Focus"];
    internal static readonly string[] EnemySources = ["WorldMouseover", "Selected", "Focus"];
    internal static bool Valid(string[] order, bool ally) => order.Length == (ally ? AllySources : EnemySources).Length &&
        order.Distinct(StringComparer.Ordinal).Count() == order.Length && order.All((ally ? AllySources : EnemySources).Contains);
    internal static IEnumerable<TargetOverride> Apply(IEnumerable<TargetOverride> overrides, string[] order) =>
        overrides.OrderBy(item => item.Source == TargetSource.Explicit ? -1 : Array.IndexOf(order, item.Source.ToString()));
}
