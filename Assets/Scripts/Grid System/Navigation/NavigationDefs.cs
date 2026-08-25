using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// The area indices configured under Navigation &gt; Areas. Costs live in that
/// window; these are just the indices, so nothing has to spell them as magic
/// numbers. Keep in step with ProjectSettings/NavMeshAreas.asset.
/// </summary>
public static class NavAreas
{
    // Unity's three built-ins.
    public const int Walkable = 0;
    public const int NotWalkable = 1;
    public const int Jump = 2;

    // Water.
    public const int Water = 3;
    public const int Sea = 4;
    public const int River = 5;
    public const int Lake = 6;
    public const int ShallowSea = 7;
    public const int DeepSea = 8;
    public const int Ocean = 9;

    /// <summary>Surface &lt;-&gt; deep transition. Submarines only.</summary>
    public const int Dive = 10;

    // Land.
    public const int Mountains = 12;

    // Air.
    public const int HighAltitude = 13;
    public const int MidAltitude = 14;
    public const int LowAltitude = 15;
    public const int OpenSky = 17;
    public const int ClosedSky = 18;

    /// <summary>Convenience mask builder: NavAreas.Mask(Ocean, ShallowSea).</summary>
    public static int Mask(params int[] areas)
    {
        int mask = 0;
        for (int i = 0; i < areas.Length; i++) mask |= 1 << areas[i];
        return mask;
    }
}

/// <summary>
/// Looks agent type IDs up by the name shown in Navigation &gt; Agents, so nothing
/// has to hard-code the hashed IDs (Submarine is -334000983 today, but that is an
/// implementation detail of whoever created the agent type).
/// </summary>
public static class NavAgentTypes
{
    public const string Humanoid = "Humanoid";
    public const string Ship = "Ship";
    public const string Submarine = "Submarine";
    public const string Aircraft = "Aircraft";

    private static readonly Dictionary<string, int> Cache = new Dictionary<string, int>();

    /// <summary>
    /// The agent type ID for a name, or 0 (Humanoid) with a warning if the agent
    /// type has not been created yet.
    /// </summary>
    public static int Id(string agentTypeName)
    {
        int id;
        if (Cache.TryGetValue(agentTypeName, out id)) return id;

        for (int i = 0; i < NavMesh.GetSettingsCount(); i++)
        {
            NavMeshBuildSettings settings = NavMesh.GetSettingsByIndex(i);
            if (NavMesh.GetSettingsNameFromID(settings.agentTypeID) != agentTypeName) continue;

            Cache[agentTypeName] = settings.agentTypeID;
            return settings.agentTypeID;
        }

        Debug.LogWarning($"NavAgentTypes: no agent type named '{agentTypeName}' - " +
                         "create it under Navigation > Agents. Falling back to Humanoid.");
        Cache[agentTypeName] = 0;
        return 0;
    }

    /// <summary>True if an agent type with this name exists.</summary>
    public static bool Exists(string agentTypeName)
    {
        for (int i = 0; i < NavMesh.GetSettingsCount(); i++)
        {
            if (NavMesh.GetSettingsNameFromID(NavMesh.GetSettingsByIndex(i).agentTypeID) == agentTypeName)
                return true;
        }
        return false;
    }

    /// <summary>Clears the cache. Agent types can be edited while the editor is running.</summary>
    public static void Invalidate()
    {
        Cache.Clear();
    }
}
