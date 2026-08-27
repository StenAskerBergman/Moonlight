using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Utility for handling missing or placeholder assets (Audio, VFX, Materials, Animators).
/// Soft-logs warnings tagged with [Asset Deliverable Missing] without halting execution or throwing fatal exceptions.
/// </summary>
public static class AssetFallback
{
    // Cache of already-logged missing assets to avoid spamming the console every frame
    private static readonly HashSet<string> LoggedMissingAssets = new HashSet<string>();

    /// <summary>
    /// Soft-logs that an expected asset deliverable is missing on a specific component.
    /// Logs once per asset/context pair to prevent console spam.
    /// </summary>
    public static void LogMissingDeliverable(string assetType, string assetName, Component context = null)
    {
        string key = $"{context?.gameObject.name ?? "Global"}_{assetType}_{assetName}";
        if (LoggedMissingAssets.Contains(key)) return;

        LoggedMissingAssets.Add(key);
        string contextName = context != null ? $" on '{context.gameObject.name}' ({context.GetType().Name})" : "";
        Debug.LogWarning($"<color=yellow>[Asset Deliverable Missing]</color> {assetType} '{assetName}' is not assigned{contextName}. Using safe fallback.");
    }

    /// <summary>
    /// Safely plays an AudioClip on the provided AudioSource. If either is missing, soft-logs and safely returns.
    /// </summary>
    public static bool SafePlayAudio(AudioSource source, AudioClip clip, string clipIdentifier, Component context = null)
    {
        if (source == null)
        {
            LogMissingDeliverable("AudioSource", "Component", context);
            return false;
        }

        if (clip == null)
        {
            LogMissingDeliverable("AudioClip", clipIdentifier, context);
            return false;
        }

        source.PlayOneShot(clip);
        return true;
    }

    /// <summary>
    /// Safely sets emission on a ParticleSystem. If null, soft-logs and safely returns.
    /// </summary>
    public static bool SafeSetParticleEmission(ParticleSystem ps, bool enabled, float rateMultiplier = 1f, Component context = null)
    {
        if (ps == null)
        {
            LogMissingDeliverable("ParticleSystem", "VFX Component", context);
            return false;
        }

        var emission = ps.emission;
        emission.enabled = enabled;
        emission.rateOverTimeMultiplier = rateMultiplier;
        return true;
    }

    /// <summary>
    /// Safely plays a particle system. If null, soft-logs and returns.
    /// </summary>
    public static bool SafePlayParticle(ParticleSystem ps, Component context = null)
    {
        if (ps == null)
        {
            LogMissingDeliverable("ParticleSystem", "VFX Component", context);
            return false;
        }

        ps.Play();
        return true;
    }

    /// <summary>
    /// Clears the logged missing assets cache (useful on scene load or tests).
    /// </summary>
    public static void ResetLogCache()
    {
        LoggedMissingAssets.Clear();
    }
}
