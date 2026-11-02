namespace WrathFork.Contract;

public enum EnemySelection { Manual, PartyCombat, AnyEnemy }

// Null preserves the existing Wrath preference; each override is independently sparse.
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
    public bool Empty => Targeting is null && TargetRange is null && AoeTargets is null && ActivityUntargetedAoe is null && CombatOnly is null && Openers is null && OpenerPotions is null;
    public RotationPreferences Over(RotationPreferences parent) => new()
    {
        Targeting = Targeting ?? parent.Targeting, TargetRange = TargetRange ?? parent.TargetRange,
        AoeTargets = AoeTargets ?? parent.AoeTargets, ActivityUntargetedAoe = ActivityUntargetedAoe ?? parent.ActivityUntargetedAoe,
        CombatOnly = CombatOnly ?? parent.CombatOnly, Openers = Openers ?? parent.Openers, OpenerPotions = OpenerPotions ?? parent.OpenerPotions,
    };
}
