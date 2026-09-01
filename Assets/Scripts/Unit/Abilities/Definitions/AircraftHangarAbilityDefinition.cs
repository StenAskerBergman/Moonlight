using UnityEngine;

/// <summary>
/// Aircraft Hangar passive ability for carrier vessels like Atlas.
/// Configures onboard hangar capacity (e.g. 2 light or 1 heavy aircraft).
/// </summary>
[CreateAssetMenu(fileName = "Aircraft Hangar Ability", menuName = "Data/Unit/Abilities/Aircraft Hangar Ability")]
public class AircraftHangarAbilityDefinition : AbilityDefinition
{
    [Header("Carrier Capacity")]
    public int lightAircraftCapacity = 2;
    public int heavyAircraftCapacity = 1;

    private void Reset()
    {
        displayName = "Aircraft Hangar";
        description = "Provides flight deck staging, service, and launch facilities for support aircraft.";
        abilityType = AbilityType.Passive;
        cooldown = 0f;
    }
}
