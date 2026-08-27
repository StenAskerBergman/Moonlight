using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Defines bounded walkable movement areas and interest nodes around building entrances/docks/workbenches.
/// Allows ambient worker agents to perform randomized, non-repeating activities around the structure.
/// </summary>
public class FeedbackMovementArea : MonoBehaviour
{
    [Header("Movement Bounding Box")]
    [SerializeField] private Vector3 areaCenter = Vector3.zero;
    [SerializeField] private Vector3 areaSize = new Vector3(3f, 0.5f, 3f);

    [Header("Specific Work / Interest Nodes (Optional)")]
    [SerializeField] private List<Transform> interestNodes = new List<Transform>();

    /// <summary>
    /// Returns a random world-space position within the designated worker movement area.
    /// </summary>
    public Vector3 GetRandomPointInArea()
    {
        Vector3 localRandom = areaCenter + new Vector3(
            Random.Range(-areaSize.x * 0.5f, areaSize.x * 0.5f),
            0f,
            Random.Range(-areaSize.z * 0.5f, areaSize.z * 0.5f)
        );

        return transform.TransformPoint(localRandom);
    }

    /// <summary>
    /// Returns a random interest node position if defined, or a random point in the area as fallback.
    /// </summary>
    public Vector3 GetRandomInterestPoint()
    {
        if (interestNodes != null && interestNodes.Count > 0)
        {
            Transform node = interestNodes[Random.Range(0, interestNodes.Count)];
            if (node != null) return node.position;
        }

        return GetRandomPointInArea();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 0.4f, 0.35f);
        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(areaCenter, areaSize);
        Gizmos.color = new Color(0.2f, 0.8f, 0.4f, 0.8f);
        Gizmos.DrawWireCube(areaCenter, areaSize);
        Gizmos.matrix = oldMatrix;

        if (interestNodes != null)
        {
            Gizmos.color = Color.yellow;
            foreach (Transform node in interestNodes)
            {
                if (node != null)
                {
                    Gizmos.DrawSphere(node.position, 0.2f);
                }
            }
        }
    }
}
