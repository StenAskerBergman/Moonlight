using UnityEngine;

/// <summary>
/// Data-driven definition for unit abilities.
/// Configures gameplay parameters, UI presentation, cooldowns, and targeting restrictions.
/// </summary>
[CreateAssetMenu(fileName = "New Ability Definition", menuName = "Data/Unit/Ability Definition")]
public class AbilityDefinition : ScriptableObject, IIdentifiable
{
    [Header("Identity")]
    [Tooltip("Namespaced identifier (e.g. 'moonlight:ability_dive').")]
    [SerializeField] private string identifier = "moonlight:ability";

    public Identifier Id => !string.IsNullOrEmpty(identifier)
        ? new Identifier(identifier)
        : new Identifier($"moonlight:{name.ToLowerInvariant().Replace(' ', '_')}");

    [Header("Display")]
    public string displayName;
    [TextArea(2, 4)]
    public string description;
    public Sprite icon;
    public string hotkeyHint;

    [Header("Behavior")]
    public AbilityType abilityType = AbilityType.Active;
    public float cooldown = 0f;
    public float range = 0f;
    public float duration = 0f;
    public CombatTargetCapabilities targetRestrictions = CombatTargetCapabilities.None;

    /// <summary>
    /// Validates whether this ability can be activated by the unit in its current state.
    /// </summary>
    public virtual bool CanActivate(Unit owner, UnitAbilities abilities)
    {
        return true;
    }

    /// <summary>
    /// Executes the ability logic on the owner unit.
    /// </summary>
    public virtual bool Execute(Unit owner, UnitAbilities abilities, Vector3? targetPosition = null, Unit targetUnit = null)
    {
        return true;
    }
}
