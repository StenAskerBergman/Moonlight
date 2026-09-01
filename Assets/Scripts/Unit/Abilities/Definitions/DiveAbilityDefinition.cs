using UnityEngine;

/// <summary>
/// Reusable Dive ability shared by all submarines.
/// Wraps and drives DiveInteraction dynamically so UI and gameplay logic remain unified.
/// </summary>
[CreateAssetMenu(fileName = "Dive Ability", menuName = "Data/Unit/Abilities/Dive Ability")]
public class DiveAbilityDefinition : AbilityDefinition
{
    private void Reset()
    {
        displayName = "Dive";
        description = "Submerge to deep water or surface. When submerged, submarine navigates underwater layers.";
        abilityType = AbilityType.Toggle;
        cooldown = 8f;
        targetRestrictions = CombatTargetCapabilities.None;
    }

    public override bool CanActivate(Unit owner, UnitAbilities abilities)
    {
        if (owner == null) return false;
        DiveInteraction dive = owner.GetComponent<DiveInteraction>();
        if (dive == null) return false;

        return dive.IsSubmerged ? dive.CanSurface() : dive.CanDive();
    }

    public override bool Execute(Unit owner, UnitAbilities abilities, Vector3? targetPosition = null, Unit targetUnit = null)
    {
        if (owner == null) return false;
        DiveInteraction dive = owner.GetComponent<DiveInteraction>();
        if (dive == null) return false;

        if (dive.IsSubmerged)
        {
            return dive.Surface();
        }
        else
        {
            return dive.Dive();
        }
    }

    public string GetDynamicVerb(Unit owner)
    {
        if (owner == null) return "Dive";
        DiveInteraction dive = owner.GetComponent<DiveInteraction>();
        if (dive != null && dive.IsSubmerged) return "Surface";
        return "Dive";
    }
}
