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
}
