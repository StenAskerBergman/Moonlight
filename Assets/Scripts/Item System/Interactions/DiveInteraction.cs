using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class DiveInteraction : MonoBehaviour, IDiveable
{
    // Submarine Profile agentTypeID - see NavMeshMovementProfile.cs header comment.
    private const int SubmarineAgentTypeID = -334000983;

    private Unit unit;
    private NavMeshAgent agent;

    public bool isSubmerged { get; private set; }

    // Cached so surfacing can restore whatever surface agent type the unit was
    // configured with (Ship Profile, etc.) instead of hardcoding one.
    private int surfaceAgentTypeID;
    private bool hasSurfaceAgentTypeID;

    public delegate void DiveStateChangedHandler(bool isSubmerged);
    public event DiveStateChangedHandler OnDiveStateChanged;

    private void Awake()
    {
        unit = GetComponent<Unit>();
        agent = GetComponent<NavMeshAgent>();
    }

    public void Dive()
    {
        if (!CanToggleDive(out string failReason))
        {
            Debug.Log($"DiveInteraction: Cannot dive - {failReason}");
            return;
        }

        isSubmerged = !isSubmerged;

        if (agent != null)
        {
            if (isSubmerged)
            {
                if (!hasSurfaceAgentTypeID)
                {
                    surfaceAgentTypeID = agent.agentTypeID;
                    hasSurfaceAgentTypeID = true;
                }

                agent.agentTypeID = SubmarineAgentTypeID;
            }
            else if (hasSurfaceAgentTypeID)
            {
                agent.agentTypeID = surfaceAgentTypeID;
            }

            // TODO: project-specific wiring - if the submarine and surface NavMeshes
            // aren't both baked/available, the agent may need to be re-placed after
            // switching agentTypeID (see UnitMovement.TryPlaceOnNavMesh).
        }

        Debug.Log($"DiveInteraction: {(isSubmerged ? "Submerging" : "Surfacing")} {gameObject.name}.");
        OnDiveStateChanged?.Invoke(isSubmerged);
    }

    /// <summary>
    /// Runs the same checks as Dive() without toggling any state. Used by UI to
    /// decide whether the dive action should be shown/enabled.
    /// </summary>
    public bool CanDive()
    {
        return CanToggleDive(out _);
    }

    private bool CanToggleDive(out string failReason)
    {
        failReason = null;

        if (unit == null)
        {
            failReason = "no Unit component found";
            return false;
        }

        // TODO: if per-unit NameType categorization (ExplorerSub / AttackSub) is
        // wired onto Unit in the future, prefer checking that here instead - moveType
        // is the only field currently available that marks a unit as a submarine.
        if (unit.moveType != MoveType.Submersible)
        {
            failReason = "unit is not a submarine type";
            return false;
        }

        // Surfacing should always be allowed for a submarine; only submerging
        // requires currently being on water.
        if (isSubmerged) return true;

        if (!IsOnWater())
        {
            failReason = "unit is not on water";
            return false;
        }

        return true;
    }

    private bool IsOnWater()
    {
        if (IslandManager.instance == null) return false;

        Island island = IslandManager.instance.GetIsland(transform.position);
        if (island == null) return false;

        GridSystem gridSystem = island.GetComponent<GridSystem>();
        if (gridSystem == null) return false;

        Cell cell = gridSystem.GetCellAtWorldPosition(transform.position);
        if (cell == null) return false;

        return IsWaterTerrain(cell.currentTerrainType);
    }

    private static bool IsWaterTerrain(Cell.TerrainType terrain)
    {
        switch (terrain)
        {
            case Cell.TerrainType.Water:
            case Cell.TerrainType.Sea:
            case Cell.TerrainType.Ocean:
            case Cell.TerrainType.Shallow:
            case Cell.TerrainType.Deep:
            case Cell.TerrainType.Abyssal:
            case Cell.TerrainType.River:
            case Cell.TerrainType.Stream:
                return true;
            default:
                return false;
        }
    }
}
