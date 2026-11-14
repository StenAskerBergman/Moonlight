// tldr: The authority over what foliage exists on one island. Everything that creates, destroys or
// changes a plant goes through here, so listeners can react without knowing who did it.
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Per-island foliage model. Owns the instances; owns no rendering.
///
/// The single choke point is deliberate: construction clearance, the player's clear tool, a logger
/// felling a trunk and any future storm event all call <see cref="Remove"/> with a cause, and
/// yield/effects/audio hang off <see cref="Removed"/> rather than off each caller. Adding another
/// way to destroy a tree later costs one enum value, not another integration per listener.
///
/// Instances are kept in a dense list with an id map and a cell map beside it, so removal is O(1)
/// and both "what is on this cell" (construction) and "what is within this radius" (forestry) are
/// cheap without a physics query.
/// </summary>
public sealed class FoliageStore
{
    private readonly List<FoliageInstance> instances = new List<FoliageInstance>();
    private readonly Dictionary<int, int> idToIndex = new Dictionary<int, int>();
    private readonly Dictionary<Vector2Int, List<int>> cellIndex = new Dictionary<Vector2Int, List<int>>();
    private readonly List<FoliageSpecies> species = new List<FoliageSpecies>();
    private readonly List<int> scratch = new List<int>();
    private int nextId = 1;

    public event Action<FoliageInstance> Added;
    public event Action<FoliageInstance, FoliageRemovalCause> Removed;
    public event Action<FoliageInstance, FoliageState> StateChanged;

    public int Count => instances.Count;
    public int SpeciesCount => species.Count;

    /// <summary>Adds a species to this island's table and returns the id instances refer to it by.</summary>
    public int RegisterSpecies(FoliageSpecies value)
    {
        if (value == null) return -1;
        int existing = species.IndexOf(value);
        if (existing >= 0) return existing;
        species.Add(value);
        return species.Count - 1;
    }

    public FoliageSpecies GetSpecies(int speciesId) =>
        speciesId >= 0 && speciesId < species.Count ? species[speciesId] : null;

    public FoliageSpecies GetSpecies(FoliageInstance instance) => GetSpecies(instance.SpeciesId);

    /// <summary>Every live instance, in no meaningful order. Do not mutate the store while iterating.</summary>
    public IReadOnlyList<FoliageInstance> Instances => instances;

    public bool TryGet(int id, out FoliageInstance instance)
    {
        if (idToIndex.TryGetValue(id, out int index)) { instance = instances[index]; return true; }
        instance = default;
        return false;
    }

    public int Add(int speciesId, Vector2Int cell, Vector3 localPosition, float yaw, float scale,
                   FoliageOrigin origin, FoliageState state = FoliageState.Living)
    {
        if (GetSpecies(speciesId) == null) return 0;

        var instance = new FoliageInstance
        {
            Id = nextId++,
            Cell = cell,
            SpeciesId = speciesId,
            State = state,
            Origin = origin,
            LocalPosition = localPosition,
            Yaw = yaw,
            Scale = scale,
        };

        int index = instances.Count;
        instances.Add(instance);
        idToIndex[instance.Id] = index;
        AttachToCell(cell, index);

        Added?.Invoke(instance);
        return instance.Id;
    }

    public bool Remove(int id, FoliageRemovalCause cause)
    {
        if (!idToIndex.TryGetValue(id, out int index)) return false;
        FoliageInstance removed = instances[index];

        DetachFromCell(removed.Cell, index);
        idToIndex.Remove(id);

        // Swap-remove keeps the list dense; the moved instance's index has to be repaired in both maps.
        int last = instances.Count - 1;
        if (index != last)
        {
            FoliageInstance moved = instances[last];
            instances[index] = moved;
            idToIndex[moved.Id] = index;
            RetargetCellSlot(moved.Cell, last, index);
        }
        instances.RemoveAt(last);

        Removed?.Invoke(removed, cause);
        return true;
    }

    /// <summary>Removes everything standing on one cell. This is what building placement calls.</summary>
    public int RemoveAll(Vector2Int cell, FoliageRemovalCause cause)
    {
        if (!cellIndex.TryGetValue(cell, out List<int> slots) || slots.Count == 0) return 0;

        // Snapshot the ids first - Remove mutates the very list being walked.
        scratch.Clear();
        for (int i = 0; i < slots.Count; i++) scratch.Add(instances[slots[i]].Id);

        int removed = 0;
        for (int i = 0; i < scratch.Count; i++) if (Remove(scratch[i], cause)) removed++;
        return removed;
    }

    /// <summary>
    /// Transitions one instance's life stage in place, keeping its id and position. Felling is this,
    /// not a delete plus an add, so a stump lands exactly where the trunk stood.
    /// </summary>
    public bool SetState(int id, FoliageState state)
    {
        if (!idToIndex.TryGetValue(id, out int index)) return false;
        FoliageInstance instance = instances[index];
        if (instance.State == state) return false;

        instance.State = state;
        instances[index] = instance;

        StateChanged?.Invoke(instance, state);
        return true;
    }

    public int QueryCell(Vector2Int cell, List<int> results)
    {
        results.Clear();
        if (!cellIndex.TryGetValue(cell, out List<int> slots)) return 0;
        for (int i = 0; i < slots.Count; i++) results.Add(instances[slots[i]].Id);
        return results.Count;
    }

    /// <summary>
    /// Ids within <paramref name="radius"/> of a MapGrid-local point, measured on the ground plane.
    /// Cells are one unit, so this only walks the cells the circle actually covers.
    /// </summary>
    public int QueryRadius(Vector3 localCentre, float radius, List<int> results,
                           FoliageFilter filter = FoliageFilter.None)
    {
        results.Clear();
        float radiusSqr = radius * radius;
        int minX = Mathf.FloorToInt(localCentre.x - radius), maxX = Mathf.CeilToInt(localCentre.x + radius);
        int minZ = Mathf.FloorToInt(localCentre.z - radius), maxZ = Mathf.CeilToInt(localCentre.z + radius);

        for (int z = minZ; z <= maxZ; z++)
        for (int x = minX; x <= maxX; x++)
        {
            if (!cellIndex.TryGetValue(new Vector2Int(x, z), out List<int> slots)) continue;
            for (int i = 0; i < slots.Count; i++)
            {
                FoliageInstance instance = instances[slots[i]];
                if (!Matches(instance, filter)) continue;
                float dx = instance.LocalPosition.x - localCentre.x;
                float dz = instance.LocalPosition.z - localCentre.z;
                if (dx * dx + dz * dz <= radiusSqr) results.Add(instance.Id);
            }
        }
        return results.Count;
    }

    /// <summary>
    /// The instance nearest a MapGrid-local point within <paramref name="maxDistance"/>, or 0.
    /// This is how the player's clear tool picks a plant without any of them having colliders.
    /// </summary>
    public int FindNearest(Vector3 localPoint, float maxDistance, FoliageFilter filter = FoliageFilter.None)
    {
        int bestId = 0;
        float bestSqr = maxDistance * maxDistance;
        int minX = Mathf.FloorToInt(localPoint.x - maxDistance), maxX = Mathf.CeilToInt(localPoint.x + maxDistance);
        int minZ = Mathf.FloorToInt(localPoint.z - maxDistance), maxZ = Mathf.CeilToInt(localPoint.z + maxDistance);

        for (int z = minZ; z <= maxZ; z++)
        for (int x = minX; x <= maxX; x++)
        {
            if (!cellIndex.TryGetValue(new Vector2Int(x, z), out List<int> slots)) continue;
            for (int i = 0; i < slots.Count; i++)
            {
                FoliageInstance instance = instances[slots[i]];
                if (!Matches(instance, filter)) continue;
                float dx = instance.LocalPosition.x - localPoint.x;
                float dz = instance.LocalPosition.z - localPoint.z;
                float sqr = dx * dx + dz * dz;
                if (sqr > bestSqr) continue;
                bestSqr = sqr;
                bestId = instance.Id;
            }
        }
        return bestId;
    }

    /// <summary>Drops every instance without raising <see cref="Removed"/>; for teardown and regeneration.</summary>
    public void Clear()
    {
        instances.Clear();
        idToIndex.Clear();
        cellIndex.Clear();
        species.Clear();
        nextId = 1;
    }

    private bool Matches(FoliageInstance instance, FoliageFilter filter)
    {
        if (filter == FoliageFilter.None) return true;

        if ((filter & FoliageFilter.LivingOnly) != 0 && instance.State != FoliageState.Living) return false;

        if ((filter & FoliageFilter.StandingOnly) != 0
            && instance.State != FoliageState.Sapling
            && instance.State != FoliageState.Living
            && instance.State != FoliageState.Dead) return false;

        if ((filter & FoliageFilter.PlayerPlacedOnly) != 0 && instance.Origin != FoliageOrigin.Player) return false;

        if ((filter & FoliageFilter.HarvestableOnly) != 0)
        {
            FoliageSpecies value = GetSpecies(instance.SpeciesId);
            if (value == null || !value.harvestable) return false;
        }
        return true;
    }

    private void AttachToCell(Vector2Int cell, int index)
    {
        if (!cellIndex.TryGetValue(cell, out List<int> slots)) cellIndex.Add(cell, slots = new List<int>(1));
        slots.Add(index);
    }

    private void DetachFromCell(Vector2Int cell, int index)
    {
        if (!cellIndex.TryGetValue(cell, out List<int> slots)) return;
        slots.Remove(index);
        if (slots.Count == 0) cellIndex.Remove(cell);
    }

    private void RetargetCellSlot(Vector2Int cell, int from, int to)
    {
        if (!cellIndex.TryGetValue(cell, out List<int> slots)) return;
        for (int i = 0; i < slots.Count; i++)
            if (slots[i] == from) { slots[i] = to; return; }
    }
}
