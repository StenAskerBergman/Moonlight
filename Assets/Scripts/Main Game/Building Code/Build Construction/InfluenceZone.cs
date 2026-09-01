using UnityEngine;

public class InfluenceZone : MonoBehaviour
{
    [SerializeField] private float radius = 15f;
    [SerializeField] private RequirementEnums.RequirementSubTypeZone zoneType = RequirementEnums.RequirementSubTypeZone.DepotZone;

    public float Radius => radius;
    public RequirementEnums.RequirementSubTypeZone ZoneType => zoneType;
    public Vector3 Center => transform.position;

    public bool ContainsPoint(Vector3 worldPoint)
    {
        Vector3 flat = worldPoint; flat.y = 0;
        Vector3 flatCenter = Center; flatCenter.y = 0;
        return Vector3.Distance(flat, flatCenter) <= radius;
    }

    public void Configure(float influenceRadius, RequirementEnums.RequirementSubTypeZone influenceType)
    {
        radius = Mathf.Max(0f, influenceRadius);
        zoneType = influenceType;
    }

    private void OnDestroy()
    {
        InfluenceManager manager = GetComponentInParent<InfluenceManager>();
        if (manager == null)
        {
            Island island = GetComponentInParent<Island>();
            if (island != null) manager = island.GetComponent<InfluenceManager>();
        }

        if (manager != null)
        {
            manager.UnregisterZone(this);
        }
    }
}
