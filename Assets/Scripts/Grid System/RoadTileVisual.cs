using UnityEngine;

public sealed class RoadTileVisual : MonoBehaviour
{
    private GameObject currentVisual;
    private GameObject currentPrefab;
    private float currentRotation;

    public void Apply(RoadTopologyResolver.Result result)
    {
        if (currentPrefab == result.Prefab && Mathf.Approximately(currentRotation, result.Rotation)) return;

        if (currentVisual != null) Destroy(currentVisual);
        currentPrefab = result.Prefab;
        currentRotation = result.Rotation;

        if (currentPrefab == null) return;
        currentVisual = Instantiate(currentPrefab, transform);
        currentVisual.name = currentPrefab.name;
        currentVisual.transform.localPosition = Vector3.zero;
        currentVisual.transform.localRotation = Quaternion.Euler(0f, currentRotation, 0f);
    }
}
