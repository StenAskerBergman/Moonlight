using System.Collections.Generic;
using UnityEngine;
using static Enums;

/// <summary>
/// Everything the lobby decides about a match, in the form the Match scene can
/// actually consume.
///
/// The fields here are deliberately limited to settings something in the Match
/// scene already reads - MapManager's generation inputs and the player's starting
/// factions. Adding fields nothing consumes yet just creates lobby controls that
/// silently do nothing.
/// </summary>
[System.Serializable]
public class MatchConfig
{
    [Tooltip("Display name for the session. Cosmetic only.")]
    public string matchName = "Skirmish";

    [Header("Map")]
    public MapManager.SpawnPattern spawnPattern = MapManager.SpawnPattern.Normal;

    [Header("Player")]
    [Tooltip("Factions the player starts the match with.")]
    public List<Faction> startingFactions = new List<Faction> { Faction.Tyc };

    [Tooltip("Whether the starter flagship begins with colonization resources (modules, tools, fish).")]
    public bool startWithResources = true;

    /// <summary>
    /// A copy, so the lobby can keep editing its own instance after handing one
    /// off - and so the match cannot mutate what the lobby still holds.
    /// </summary>
    public MatchConfig Copy()
    {
        return new MatchConfig
        {
            matchName          = matchName,
            spawnPattern       = spawnPattern,
            startingFactions   = new List<Faction>(startingFactions),
            startWithResources = startWithResources
        };
    }

    public override string ToString()
    {
        return $"MatchConfig(\"{matchName}\", {spawnPattern}, " +
               $"factions: {string.Join("/", startingFactions)})";
    }
}
