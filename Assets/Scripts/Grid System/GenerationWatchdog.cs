using System;
using System.Diagnostics;
using UnityEngine;

/// <summary>
/// Execution guard for procedural terrain generation.
/// Tracks generation phases and triggers an auto-break if generation exceeds the specified timeout.
/// </summary>
public static class GenerationWatchdog
{
    private static Stopwatch stopwatch;
    private static float timeoutMs = 8000f; // 8 seconds default

    public static string CurrentPhase { get; private set; } = "Idle";
    public static string CurrentChunk { get; private set; } = "None";
    public static bool IsRunning { get; private set; } = false;

    public static void Begin(float timeoutSeconds)
    {
        stopwatch = Stopwatch.StartNew();
        timeoutMs = Mathf.Max(0.1f, timeoutSeconds) * 1000f;
        CurrentPhase = "Starting";
        CurrentChunk = "MapManager";
        IsRunning = true;
    }

    public static void SetPhase(string chunk, string phase)
    {
        CurrentChunk = chunk ?? "Unknown";
        CurrentPhase = phase ?? "Processing";
        CheckTimeout();
    }

    public static void CheckTimeout()
    {
        if (!IsRunning || stopwatch == null) return;

        if (stopwatch.ElapsedMilliseconds > timeoutMs)
        {
            IsRunning = false;
            string errorMsg = $"[Map Generation Auto-Break] Generation exceeded timeout of {timeoutMs / 1000f:F1}s (Elapsed: {stopwatch.ElapsedMilliseconds} ms)! Stopped at Chunk: '{CurrentChunk}', Phase: '{CurrentPhase}'.";
            UnityEngine.Debug.LogError(errorMsg);
            throw new TimeoutException(errorMsg);
        }
    }

    public static void Complete()
    {
        IsRunning = false;
        if (stopwatch != null) stopwatch.Stop();
    }
}
