using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class SelectionOutlineTarget : MonoBehaviour
{
    public readonly struct OutlineRenderer
    {
        public readonly Renderer Renderer;
        public readonly int SubMeshCount;

        public OutlineRenderer(Renderer renderer, int subMeshCount)
        {
            Renderer = renderer;
            SubMeshCount = subMeshCount;
        }
    }

    private static readonly HashSet<SelectionOutlineTarget> activeTargets = new HashSet<SelectionOutlineTarget>();

    [SerializeField]
    private bool selected;

    private readonly List<OutlineRenderer> renderers = new List<OutlineRenderer>();

    public bool IsSelected => selected;

    private void Awake()
    {
        RefreshRenderers();
    }

    private void OnEnable()
    {
        if (selected)
            activeTargets.Add(this);
    }

    private void OnDisable()
    {
        activeTargets.Remove(this);
    }

    private void OnTransformChildrenChanged()
    {
        RefreshRenderers();
    }

    public void SetSelected(bool value)
    {
        if (selected == value)
            return;

        selected = value;

        if (!isActiveAndEnabled)
            return;

        if (selected)
            activeTargets.Add(this);
        else
            activeTargets.Remove(this);
    }

    // Unity only calls OnTransformChildrenChanged for direct-child changes on this
    // transform, not for deeper structural edits (e.g. a building upgrade adding parts
    // several levels down). Call this after mutating the hierarchy under a selectable
    // root when those changes need to be reflected in the outline immediately.
    public void RefreshRenderers()
    {
        renderers.Clear();

        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null)
                continue;

            renderers.Add(new OutlineRenderer(renderer, GetSubMeshCount(renderer)));
        }
    }

    private static int GetSubMeshCount(Renderer renderer)
    {
        if (renderer is SkinnedMeshRenderer skinned && skinned.sharedMesh != null)
            return skinned.sharedMesh.subMeshCount;

        if (renderer.TryGetComponent(out MeshFilter meshFilter) && meshFilter.sharedMesh != null)
            return meshFilter.sharedMesh.subMeshCount;

        return 1;
    }

    public static void CollectRenderers(List<OutlineRenderer> destination)
    {
        destination.Clear();

        foreach (SelectionOutlineTarget target in activeTargets)
        {
            if (target == null)
                continue;

            destination.AddRange(target.renderers);
        }
    }
}
