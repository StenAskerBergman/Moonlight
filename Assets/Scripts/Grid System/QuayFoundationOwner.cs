using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Building lifetime bridge for shared quay ownership. Removing this building releases
/// only its automatic references; any manual ownership on the same cells survives.
/// </summary>
[DisallowMultipleComponent]
public sealed class QuayFoundationOwner : MonoBehaviour
{
    private QuaySystem quay;
    private int ownerId;
    private List<Vector2Int> coordinates;

    public void Configure(QuaySystem system, int id, List<Vector2Int> ownedCoordinates)
    {
        quay = system;
        ownerId = id;
        coordinates = ownedCoordinates;
    }

    private void OnDestroy()
    {
        if (quay != null) quay.ReleaseAutomaticFoundation(ownerId, coordinates);
    }
}
