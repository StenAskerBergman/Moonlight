using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[Serializable]
public struct SettlementCost
{
    public ItemData item;
    public int amount;
}

[RequireComponent(typeof(Unit))]
[RequireComponent(typeof(UnitInventory))]
public class Settlement : MonoBehaviour
{
    [SerializeField] private float settleRange = 15f;
    [SerializeField] private bool transferEntireInventoryOnSettlement = true;

    private Unit unit;
    private UnitInventory unitInventory;

    private bool tryingToSettle;

    [SerializeField] private LineRenderer settleRangeRenderer;
    [SerializeField] private int circleSegments = 64;

    [Header("Range Visual")]
    [Tooltip("How far past the settle circle the darkened shroud reaches.")]
    [SerializeField] private float shroudOuterScale = 14f;
    [SerializeField] private Color shroudColor = new Color(0f, 0f, 0f, 0.55f);
    [SerializeField] private Color ringColor = Color.white;
    [SerializeField] private float ringWidth = 0.35f;

    private GameObject shroudObject;
    private Mesh shroudMesh;
    private float builtForRange = -1f;

    /// <summary>
    /// The one number that defines this vessel's founding influence: the white circle is
    /// drawn at it, and harbor placement is accepted only inside it.
    /// </summary>
    public float SettleRange => settleRange > 0f ? settleRange : InfluenceManager.BoatFoundingRange;

    private void Awake()
    {
        unit = GetComponent<Unit>();
        unitInventory = GetComponent<UnitInventory>();

        // The visuals are built in code rather than wired on the prefab. They used to
        // depend on an Inspector reference that was never assigned, so the range circle
        // simply never existed at runtime.
        EnsureRangeVisuals();
        SetRangeVisualsVisible(false);
    }

    public bool TryingToSettle()
    {
        return tryingToSettle;
    }


    // black outside radius
    // highlight selected settler
    public void BeginSettlement()
    {
        tryingToSettle = true;
        EnsureRangeVisuals();
        SetRangeVisualsVisible(true);
    }

    public void CancelSettlement()
    {
        tryingToSettle = false;
        SetRangeVisualsVisible(false);
    }

    private void LateUpdate()
    {
        if (!tryingToSettle) return;

        // The circle and shroud describe a patch of sea, not part of the hull, so they
        // stay world-aligned while the vessel yaws and pitches under them.
        if (settleRangeRenderer != null) settleRangeRenderer.transform.rotation = Quaternion.identity;
        if (shroudObject != null) shroudObject.transform.rotation = Quaternion.identity;
    }

    public bool CanSettle(Island island, out string reason)
    {
        if (unit == null)
        {
            reason = "Unit does not exist.";
            return false;
        }

        if (island == null)
        {
            reason = unit.moveType == MoveType.Submersible
                ? "No plateau in range."
                : "No island in range.";

            return false;
        }

        float distance = Vector3.Distance(
            transform.position,
            island.transform.position
        );

        if (distance > settleRange)
        {
            reason = "Out of settlement range.";
            return false;
        }

        if (unitInventory == null)
        {
            reason = "Unit has no inventory.";
            return false;
        }

        reason = null;
        return true;
    }

    //Settlement succeeds
    // -> Always deduct construction costs
    // -> If transferEntireInventoryOnSettlement == true
    // -> move all remaining ship cargo into settlement
    public void CompleteSettlement(BaseStorageManager islandStorage)
    {
        // Optionally transfer everything remaining from the ship into island storage
        if (transferEntireInventoryOnSettlement && islandStorage != null)
        {
            Dictionary<ItemData, int> remaining = unitInventory.GetAllItems();
            if (remaining != null)
            {
                // Snapshot the keys so we can modify the inventory during iteration
                var entries = new List<KeyValuePair<ItemData, int>>(remaining);
                foreach (var kvp in entries)
                {
                    if (kvp.Key == null || kvp.Value <= 0)
                        continue;

                    if (islandStorage.CanAddItem(kvp.Key, kvp.Value))
                    {
                        islandStorage.AddItem(kvp.Key, kvp.Value);
                        unitInventory.RemoveItem(kvp.Key, kvp.Value);
                        Debug.Log($"<color=green>Settlement: Transferred {kvp.Value}x {kvp.Key.displayName} to island storage.</color>");
                    }
                    else
                    {
                        Debug.LogWarning($"Settlement: Could not transfer {kvp.Value}x {kvp.Key.displayName} — island storage full.");
                    }
                }
            }
        }
    }

    private void DrawSettleRange()
    {
        settleRangeRenderer.positionCount = circleSegments + 1;
        settleRangeRenderer.loop = true;

        for (int i = 0; i <= circleSegments; i++)
        {
            float angle = (float)i / circleSegments * Mathf.PI * 2f;

            Vector3 position = new Vector3(
                Mathf.Cos(angle) * SettleRange,
                0f,
                Mathf.Sin(angle) * SettleRange
            );

            settleRangeRenderer.SetPosition(i, position);
        }
    }

    #region Range Visual

    private void SetRangeVisualsVisible(bool visible)
    {
        if (settleRangeRenderer != null) settleRangeRenderer.enabled = visible;
        if (shroudObject != null) shroudObject.SetActive(visible);
    }

    private void EnsureRangeVisuals()
    {
        if (settleRangeRenderer == null)
        {
            GameObject ringObject = new GameObject("Settle Range Ring");
            ringObject.transform.SetParent(transform, false);

            settleRangeRenderer = ringObject.AddComponent<LineRenderer>();
            settleRangeRenderer.useWorldSpace = false;
            settleRangeRenderer.shadowCastingMode = ShadowCastingMode.Off;
            settleRangeRenderer.receiveShadows = false;
            settleRangeRenderer.sharedMaterial = OverlayMaterial.Create(ringColor);
        }

        settleRangeRenderer.startWidth = ringWidth;
        settleRangeRenderer.endWidth = ringWidth;
        settleRangeRenderer.startColor = ringColor;
        settleRangeRenderer.endColor = ringColor;

        if (shroudObject == null)
        {
            shroudObject = new GameObject("Settle Range Shroud");
            shroudObject.transform.SetParent(transform, false);

            shroudObject.AddComponent<MeshFilter>();
            MeshRenderer shroudRenderer = shroudObject.AddComponent<MeshRenderer>();
            shroudRenderer.shadowCastingMode = ShadowCastingMode.Off;
            shroudRenderer.receiveShadows = false;
            shroudRenderer.sharedMaterial = OverlayMaterial.Create(shroudColor);
        }

        if (!Mathf.Approximately(builtForRange, SettleRange))
        {
            DrawSettleRange();

            shroudMesh = BuildShroudMesh(SettleRange, SettleRange * Mathf.Max(2f, shroudOuterScale), circleSegments);
            shroudObject.GetComponent<MeshFilter>().sharedMesh = shroudMesh;
            builtForRange = SettleRange;
        }
    }

    /// <summary>
    /// A flat ring covering everything from the settle radius outwards. The hole in the
    /// middle is what makes the buildable disk read as see-through while the world
    /// around it is dimmed.
    /// </summary>
    private static Mesh BuildShroudMesh(float innerRadius, float outerRadius, int segments)
    {
        segments = Mathf.Max(8, segments);

        Vector3[] vertices = new Vector3[(segments + 1) * 2];
        int[] triangles = new int[segments * 6];

        for (int i = 0; i <= segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);

            vertices[i * 2] = new Vector3(cos * innerRadius, 0f, sin * innerRadius);
            vertices[i * 2 + 1] = new Vector3(cos * outerRadius, 0f, sin * outerRadius);
        }

        for (int i = 0; i < segments; i++)
        {
            int v = i * 2;
            int t = i * 6;

            triangles[t] = v;
            triangles[t + 1] = v + 1;
            triangles[t + 2] = v + 3;

            triangles[t + 3] = v;
            triangles[t + 4] = v + 3;
            triangles[t + 5] = v + 2;
        }

        Mesh mesh = new Mesh { name = "SettleRangeShroud" };
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        return mesh;
    }

    #endregion

    // build a circle around the unit using the line renderer
    // add a shader to black out around the circle 
    // highlight the unit with the build placement order 

    /* 
     * https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Vector3.Distance.html
     * https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Transform-position.html
     * https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Component.GetComponent.html
     * https://docs.unity3d.com/2022.3/Documentation/ScriptReference/GameObject.SetActive.html 
     * https://docs.unity3d.com/2022.3/Documentation/ScriptReference/LineRenderer.html
     * https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Material.SetVector.html
     */
}
