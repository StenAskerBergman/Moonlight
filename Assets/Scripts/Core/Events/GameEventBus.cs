using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Minimal static typed event bus. Events are readonly structs, published by value.
/// Subscribe in OnEnable, unsubscribe in OnDisable.
///
/// Static subscriber lists are cleared between play sessions via
/// [RuntimeInitializeOnLoadMethod(SubsystemRegistration)] to prevent leaks —
/// the same class of problem RuntimeNavMeshBaker.OnDestroy guards against on a
/// per-event basis.
/// </summary>
public static class GameEventBus
{
    // Each entry is a boxed List<Action<T>> keyed by event type.
    private static readonly Dictionary<Type, object> s_subscribers =
        new Dictionary<Type, object>();

    /// <summary>
    /// Registers a handler for events of type T.
    /// Call in OnEnable; pair with Unsubscribe in OnDisable.
    /// </summary>
    public static void Subscribe<T>(Action<T> handler) where T : struct
    {
        var type = typeof(T);
        if (!s_subscribers.TryGetValue(type, out var raw))
        {
            raw = new List<Action<T>>();
            s_subscribers[type] = raw;
        }
        ((List<Action<T>>)raw).Add(handler);
    }

    /// <summary>
    /// Removes a previously registered handler for events of type T.
    /// Call in OnDisable to match a Subscribe in OnEnable.
    /// </summary>
    public static void Unsubscribe<T>(Action<T> handler) where T : struct
    {
        var type = typeof(T);
        if (s_subscribers.TryGetValue(type, out var raw))
        {
            ((List<Action<T>>)raw).Remove(handler);
        }
    }

    /// <summary>
    /// Publishes an event to all registered handlers. Iterates backwards so
    /// handlers may safely unsubscribe during delivery without skipping entries.
    /// </summary>
    public static void Publish<T>(in T evt) where T : struct
    {
        var type = typeof(T);
        if (!s_subscribers.TryGetValue(type, out var raw)) return;

        var list = (List<Action<T>>)raw;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            try
            {
                list[i].Invoke(evt);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }
    }

    /// <summary>
    /// Clears all subscriber lists between play sessions / domain reloads.
    /// Without this, delegates from a previous play session survive and fire
    /// into destroyed objects.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        s_subscribers.Clear();
    }
}
