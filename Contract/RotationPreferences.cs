namespace WrathFork.Contract;

public enum EnemySelection { Manual, PartyCombat, AnyEnemy }

// Null inherits from Accord global defaults; role and class overrides stay sparse.
public sealed class RotationPreferences
{
    [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
    public EnemySelection? Targeting { get; set; }
    [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
    public int? TargetRange { get; set; }
    [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
    public int? AoeTargets { get; set; }
    [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
    public bool? ActivityUntargetedAoe { get; set; }
    [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
    public bool? CombatOnly { get; set; }
    [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
    public bool? Openers { get; set; }
    [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
    public bool? OpenerPotions { get; set; }
    [Newtonsoft.Json.JsonIgnore]
    public bool Empty => Targeting is null && TargetRange is null && AoeTargets is null && ActivityUntargetedAoe is null && CombatOnly is null && Openers is null && OpenerPotions is null && IncludeHealing is null && HealingGcds is null;
    [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
    public bool? IncludeHealing { get; set; }
    [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
    public bool? HealingGcds { get; set; }
    public static RotationPreferences Defaults() => new()
    {
        Targeting = EnemySelection.PartyCombat, TargetRange = 30, AoeTargets = 3,
        ActivityUntargetedAoe = false, CombatOnly = true, Openers = true, OpenerPotions = false,
        IncludeHealing = false, HealingGcds = false,
    };
    public RotationPreferences Over(RotationPreferences parent) => new()
    {
        IncludeHealing = IncludeHealing ?? parent.IncludeHealing, HealingGcds = HealingGcds ?? parent.HealingGcds,
        Targeting = Targeting ?? parent.Targeting, TargetRange = TargetRange ?? parent.TargetRange,
        AoeTargets = AoeTargets ?? parent.AoeTargets, ActivityUntargetedAoe = ActivityUntargetedAoe ?? parent.ActivityUntargetedAoe,
        CombatOnly = CombatOnly ?? parent.CombatOnly, Openers = Openers ?? parent.Openers, OpenerPotions = OpenerPotions ?? parent.OpenerPotions,
    };
}
