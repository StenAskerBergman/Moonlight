using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generic, mod-ready registry for data definitions.
/// Allows core systems and future mod loaders to register, query, and enumerate definitions by Identifier.
/// Enforces explicit duplicate conflict policies to prevent accidental overwrites.
/// </summary>
public class Registry<T> where T : class
{
    public enum DuplicatePolicy
    {
        ErrorAndReject,    // Rejects duplicate and logs error (default for core registration)
        WarnAndOverwrite,  // Overwrites existing entry with a warning (useful for intentional mod overrides)
        IgnoreDuplicate    // Silently keeps original entry
    }

    private readonly Dictionary<Identifier, T> _entries = new Dictionary<Identifier, T>();
    private readonly string _registryName;
    private bool _isFrozen = false;

    public string RegistryName => _registryName;
    public bool IsFrozen => _isFrozen;
    public int Count => _entries.Count;
    public IEnumerable<T> AllEntries => _entries.Values;
    public IEnumerable<Identifier> AllIds => _entries.Keys;

    public Registry(string registryName)
    {
        _registryName = registryName;
    }

    /// <summary>
    /// Registers an entry using its IIdentifiable ID.
    /// </summary>
    public bool Register(T entry, DuplicatePolicy policy = DuplicatePolicy.ErrorAndReject)
    {
        if (entry is IIdentifiable identifiable)
        {
            return Register(identifiable.Id, entry, policy);
        }

        Debug.LogError($"[Registry] Cannot auto-register '{entry}' in {_registryName}: Does not implement IIdentifiable. Pass Identifier explicitly.");
        return false;
    }

    /// <summary>
    /// Registers an entry with an explicit Identifier and conflict policy.
    /// </summary>
    public bool Register(Identifier id, T entry, DuplicatePolicy policy = DuplicatePolicy.ErrorAndReject)
    {
        if (_isFrozen)
        {
            Debug.LogError($"[Registry] Cannot register '{id}' — {_registryName} is frozen.");
            return false;
        }

        if (id.IsEmpty || entry == null)
        {
            Debug.LogWarning($"[Registry] Attempted to register invalid entry or empty ID in {_registryName}.");
            return false;
        }

        if (_entries.TryGetValue(id, out T existing))
        {
            switch (policy)
            {
                case DuplicatePolicy.ErrorAndReject:
                    Debug.LogError($"[Registry] Duplicate registration rejected for '{id}' in {_registryName}. Existing: '{existing}', New: '{entry}'.");
                    return false;

                case DuplicatePolicy.WarnAndOverwrite:
                    Debug.LogWarning($"[Registry] Overwriting existing entry for '{id}' in {_registryName}. Old: '{existing}', New: '{entry}'.");
                    _entries[id] = entry;
                    return true;

                case DuplicatePolicy.IgnoreDuplicate:
                    return false;
            }
        }

        _entries[id] = entry;
        return true;
    }

    /// <summary>
    /// Retrieves the entry with the specified Identifier, or null if missing.
    /// </summary>
    public T Get(Identifier id)
    {
        if (_entries.TryGetValue(id, out T value))
        {
            return value;
        }

        Debug.LogError($"[Registry] Identifier '{id}' was not found in {_registryName}.");
        return null;
    }

    /// <summary>
    /// Attempts to retrieve the entry with the specified Identifier.
    /// </summary>
    public bool TryGet(Identifier id, out T value)
    {
        return _entries.TryGetValue(id, out value);
    }

    /// <summary>
    /// Returns true if an entry exists for the given Identifier.
    /// </summary>
    public bool Contains(Identifier id)
    {
        return _entries.ContainsKey(id);
    }

    /// <summary>
    /// Freezes the registry to prevent further runtime modifications.
    /// </summary>
    public void Freeze()
    {
        _isFrozen = true;
    }

    /// <summary>
    /// Clears the registry (primarily for test cleanup or reloads).
    /// </summary>
    public void Clear()
    {
        _entries.Clear();
        _isFrozen = false;
    }
}
