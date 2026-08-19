using System.Collections.Generic;
using UnityEngine;

    // TODO: 
    //  Transport Ships
    //  Transport Planes
    //  Cargo Ships
    //  Cargo Planes 


public enum NameType
{
    // Subs
    ExplorerSub,
    AttackSub,

    // Ships
    Tradeships,
    Warship,
    
    // Units
    Ship,
    Plane,
    
    // Locations
    Cities,
    Islands,

    // Quest
    People,
    Float,
}

public class NameGenerator
{
    private static Dictionary<NameType, List<string>> namePool = new Dictionary<NameType, List<string>>
    {
        // Submarines - ES / AS 
        { NameType.ExplorerSub, new List<string> { 
            "Nemo", "Dolphine", "Whale", "Tuna", "Electric Eel", "Manatee",
            "Jellyfish", "Seahorse", "Starfish", "Bluefin Tuna", } },
        { NameType.AttackSub, new List<string> {
            "Nautilus", "Barracuda", "Seawolf", "Kraken",  "Crocodile",
            "Tigerfish", "Piranha", "Pike", "Basking Shark", "SeaSerpent", } },


        // Ships - TS / WS
        { NameType.Tradeships, new List<string> {
            "Mule", "Drake", "Crab", "Walrus", "Hippopotamus", "Blue Whale",
            "Beluga", "Manta Ray", "Elephant Seal", "Giant Tortoise", } },

        { NameType.Warship, new List<string> {
            "Alligator", "Orca", "Sealion", "Shark", "Kingcrab", "Crawfish",
            "Hammerhead", "Swordfish","SeaLion" } },

        // Units - S / P 
        { NameType.Ship, new List<string> { 
            "Seal", "Turtle", "Angler", "Catfish", "Carp", "Rudderfish", "Marulk",
            "Salmon", "Trout", "Bass", "Haddock", "Mackerel", "Sardine", "Herring",
            "SeaOtter", "SeaHawk", "SeaRaven", "SeaViper", "SeaDragon", "Kaifish"
        } },
        { NameType.Plane, new List<string> {
            "Cirrus", "Altocumulus", "Cirrocumulus", "Cirrostratus", "Cumulonimbus",
            "Stratocumulus", "Cumulostratus", "Nimbostratus", "Pileus", "Contrail"
        } },

    
        // Location Names
        { NameType.Cities, new List<string> {

            "Island 33", // Legendary Status
            "El Dorado", "Nova Vista", "Elysium", "Astroville", "Havenbrook", "Aetherburg",
            "Zenithia", "Skyhaven", "Solaris", "Crystalopolis", "Verdantville", "Avalonia",
            "Nebula City", "Auroratown", "Eclipseburg", "Radiantopolis", "Silvershore",
            "Thunderpeak", "Embermere", "Polarisburg", "Lumina",
        } },

        { NameType.Islands, new List<string> {

            "Island 33", // Legendary Status
            "Rapture", "Aqua Isle", "Emerald Haven", "Zephyr Bay", "Sapphire Atoll", "Terra Lagoon",
            "Solstice Bay", "Mystic Isle", "Eternal Reef", "Crimson Cove", "Aurora Arch", "Ixtal",
            "Iridescent Peninsula", "Paradise Cay", "Chronos Isle", "Tropicana Island", "Luminara",
            "Ruby Beach", "Australium Peak", "Sapphire Peninsula", "Serenity Isle", "Moonlit Mirage"
        } },

        // Quest - P / F
        { NameType.People, new List<string> {
            "John", "Sarah", "Mike", "Emma", "Andrew", "Yang", "Cindy", "Sam", "Ryan",
            "Claire", "Grace", "Oliver", "Sophia", "Ethan", "Lily", "Daniel", "Olivia",
            "Lucas", "Nora", "Caleb", "Ella", "William", "Mia", "Elijah", "Chloe", "Ed",
            "Benjamin", "Logan", "Avery", "Matthew", "Zoe", "Tyrone", "Ava", "Arnold",
        } },
        { NameType.Float, new List<string> {
            "Cargo", "Wreck", "Boats", "Emergancy Boat",
        } },
    };

    public static string RandomNameSelector(NameType type)
    {
        int index = UnityEngine.Random.Range(0, namePool[type].Count);
        return namePool[type][index];
    }
}

public class UnitManager : MonoBehaviour
{
    public GameObject unitPrefab;

    void Start()
    {
        // Example usage
        // string randomStarterName = NameGenerator.RandomNameSelector(NameType.Ship);
        // Debug.Log("Start Test - Generated Name: " + randomStarterName);
    }

    public void CreateUnit(Vector3 position, string name)
    {
        // Unit Will Creates its own name using its own, Start()
        GameObject unitObject = Instantiate(unitPrefab, position, Quaternion.identity);
        Unit unitComponent = unitObject.GetComponent<Unit>();
        if (unitComponent != null)
        {
            unitComponent.SetDisplayName(name);
        }
    }
}
