using System;
using UnityEngine;

/// <summary>
/// Lightweight, serializable namespaced identifier (e.g. 'core:coastal_ridge', 'modname:volcanic_ridge').
/// Used across Moonlight to decouple entities, resources, and terrain stages from closed C# enums.
/// </summary>
[System.Serializable]
public struct Identifier : IEquatable<Identifier>, IComparable<Identifier>
{
    [SerializeField] private string _value;

    public const string DefaultNamespace = "core";
    public const string EmptyId = "core:empty";

    public string Namespace
    {
        get
        {
            if (string.IsNullOrEmpty(_value)) return DefaultNamespace;
            int colonIndex = _value.IndexOf(':');
            return colonIndex >= 0 ? _value.Substring(0, colonIndex) : DefaultNamespace;
        }
    }

    public string Path
    {
        get
        {
            if (string.IsNullOrEmpty(_value)) return "empty";
            int colonIndex = _value.IndexOf(':');
            return colonIndex >= 0 ? _value.Substring(colonIndex + 1) : _value;
        }
    }

    public string FullId => !string.IsNullOrEmpty(_value) ? _value : EmptyId;
    public bool IsEmpty => string.IsNullOrEmpty(_value) || _value == EmptyId;

    public Identifier(string namespacedId)
    {
        if (string.IsNullOrWhiteSpace(namespacedId))
        {
            _value = EmptyId;
            return;
        }

        string trimmed = namespacedId.Trim().ToLowerInvariant();
        int colonIndex = trimmed.IndexOf(':');
        if (colonIndex >= 0)
        {
            string ns = trimmed.Substring(0, colonIndex);
            string path = trimmed.Substring(colonIndex + 1);
            if (string.IsNullOrEmpty(ns)) ns = DefaultNamespace;
            if (string.IsNullOrEmpty(path)) path = "empty";
            _value = $"{ns}:{path}";
        }
        else
        {
            _value = $"{DefaultNamespace}:{trimmed}";
        }
    }

    public Identifier(string @namespace, string path)
    {
        string ns = string.IsNullOrWhiteSpace(@namespace) ? DefaultNamespace : @namespace.Trim().ToLowerInvariant();
        string p = string.IsNullOrWhiteSpace(path) ? "empty" : path.Trim().ToLowerInvariant();
        _value = $"{ns}:{p}";
    }

    public static Identifier Empty => new Identifier(EmptyId);

    public static implicit operator Identifier(string id) => new Identifier(id);
    public static implicit operator string(Identifier id) => id.FullId;

    public bool Equals(Identifier other) => string.Equals(FullId, other.FullId, StringComparison.OrdinalIgnoreCase);
    public override bool Equals(object obj) => obj is Identifier other && Equals(other);
    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(FullId);
    public override string ToString() => FullId;
    public int CompareTo(Identifier other) => string.Compare(FullId, other.FullId, StringComparison.OrdinalIgnoreCase);

    public static bool operator ==(Identifier left, Identifier right) => left.Equals(right);
    public static bool operator !=(Identifier left, Identifier right) => !left.Equals(right);
}
