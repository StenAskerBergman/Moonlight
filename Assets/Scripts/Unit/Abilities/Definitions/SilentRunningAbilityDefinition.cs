using UnityEngine;

/// <summary>
/// Silent Running passive ability for stealth cargo/recon submarines like Sisyphus.
/// Greatly reduces enemy detection visibility while submerged.
/// </summary>
[CreateAssetMenu(fileName = "Silent Running Ability", menuName = "Data/Unit/Abilities/Silent Running Ability")]
public class SilentRunningAbilityDefinition : AbilityDefinition
{
    [Header("Stealth Modifier")]
    [Range(0f, 1f)]
    public float detectionModifierWhenSubmerged = 0.1f;

    private void Reset()
    {
        displayName = "Silent Running";
        description = "Baffles propulsion noise and thermal output, significantly reducing detection range while submerged.";
        abilityType = AbilityType.Passive;
        cooldown = 0f;
    }
}
