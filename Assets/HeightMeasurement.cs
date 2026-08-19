using UnityEngine;

public class HeightMeasurement : MonoBehaviour
{
    public float heightThreshold = 2f;
    public LayerMask groundLayer;
    public bool isTooClose = false;
    public bool logDetails = false;

    private Transform groundTransform = null;

    void FixedUpdate()
    {
        // Perform raycast both downwards and upwards to detect ground
        bool groundAbove = Physics.Raycast(transform.position, Vector3.up, out RaycastHit hitAbove, Mathf.Infinity, groundLayer);
        bool groundBelow = Physics.Raycast(transform.position, Vector3.down, out RaycastHit hitBelow, Mathf.Infinity, groundLayer);

        // Determine the closest hit and direction of the ground
        RaycastHit hit = groundAbove ? hitAbove : hitBelow;
        bool groundExists = groundAbove || groundBelow;

        if (groundExists)
        {
            // Update the ground transform reference
            groundTransform = hit.transform;

            // Calculate the distance to the ground
            float distanceToGround = hit.distance;

            // Determine position relative to ground
            bool isAbove = transform.position.y > hit.point.y + heightThreshold;
            bool isBelow = transform.position.y < hit.point.y - heightThreshold;

            isTooClose = isBelow;

            // Logging for debugging purposes
            if (logDetails)
            {
                Debug.Log($"Hit: {hit.collider.name}, Distance: {distanceToGround}, Position: {(isAbove ? "Above" : "Below")} ground");
            }

            // Respond to position relative to ground (example: adjust position slightly)
            if (isBelow)
            {
                float adjustment = 0.1f; // Simple upward nudge
                transform.position += new Vector3(0f, adjustment, 0f);
            }
        }
        else
        {
            if (logDetails) Debug.Log("No ground detected.");
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawRay(transform.position, Vector3.down * 100f);

        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, Mathf.Infinity, groundLayer))
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(hit.point, 0.1f);
        }
    }
}
