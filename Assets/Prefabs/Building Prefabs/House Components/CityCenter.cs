using System.Collections.Generic;
using UnityEngine;
using static Enums;

public class CityCenter : MonoBehaviour
{
    public Enums.Faction BuildingFaction;


    public float influenceRadius;
    public List<ResidentialHouse> connectedHouses;

    // public int MaxResidents; // City Center has no Max Residents
    public int CurrentResidents;
    private currentUpgradeMode _currentUpgradeMode = currentUpgradeMode.Automatic;

    private void Awake()
    {
        InfluenceZone zone = GetComponent<InfluenceZone>();
        if (zone == null) zone = gameObject.AddComponent<InfluenceZone>();
        zone.Configure(influenceRadius > 0f ? influenceRadius : 15f,
            RequirementEnums.RequirementSubTypeZone.DepotZone);

        if (GetComponent<WarehouseLogisticsScheduler>() == null)
        {
            gameObject.AddComponent<WarehouseLogisticsScheduler>();
        }
    }

    // Other properties related to residents, like happiness, needs, etc.
    private void Start()
    {
        currentUpgradeMode _currentUpgradeMode = currentUpgradeMode.Automatic;
        UpgradeMode(_currentUpgradeMode);
    }

    #region Upgrade Related
    private void RedLine ()
    {
        // Control What Building Can't Upgrade
        // By marking them with a RedLine
        // save this for later...

    }
    private enum currentUpgradeMode 
    {
        Automatic,  // Natural Over time
        RedLine,    // Exclude by Player
        Manual,     // Handled by Player
    }
    private void FixedUpdate()
    {

        UpgradeMode(_currentUpgradeMode);
    }

    private void UpgradeMode (currentUpgradeMode _currentUpgradeMode)
    {
        // Control How Residential Buildings Upgrade
        switch (_currentUpgradeMode)
        {
            case currentUpgradeMode.Automatic:
                // Check connected houses In Range
                // to upgrade them Natural Overtime
            break;
            
                case currentUpgradeMode.RedLine:   
                    // Check for connected houses and
                    // upgrade them if not marked with 
                    // a RedLine by the player
                break;

                    case currentUpgradeMode.Manual:
                        // Player Checks and Upgrades
                    break;

                        default:
                            // Handle Neutral is always default behavior
                
                        break;
        }
    }
    #endregion

    public void UpdateResidentNeeds()
    {

        switch (BuildingFaction)
        {
            case Faction.Tyc:
                // Handle Tycoons specific needs


            break;

            case Faction.Eco:
                // Handle Ecos specific needs



            break;


            case Faction.Sci:
                // Handle Ecos specific needs
            
            
            break;

                // ... handle other factions

            default:
                // Handle Neutral or default behavior
                Debug.Log(" City Center has no Faction assigned. ");
            break;
        }
    }

    // Other methods to handle residents...
    public void CheckForConnectedHouses()
    {
        // Check within radius for any houses and add to connectedHouses list
    }

}
