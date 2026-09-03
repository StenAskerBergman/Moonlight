using UnityEngine;

/// <summary>
/// The rotation authority for whatever it is attached to - in practice the building
/// blueprint. Scrolling turns the blueprint in 90 degree steps.
///
/// It composes two rotations rather than owning one, because something else usually wants
/// a say in where the blueprint starts out: BuildingPreview asks a harbor blueprint to
/// face away from the island. That used to be written straight to transform.rotation
/// every frame from BuildingPreview.Update, which silently overwrote whatever the player
/// had scrolled to and made blueprints appear unrotatable. Now the facing is a BASE
/// rotation this composes with, and it stops being applied once the player takes over.
/// </summary>
public class BuildingRotator : MonoBehaviour
{
    [SerializeField] private int rotationAngleAmount = 90;
    [Tooltip("Turns the blueprint one step. The scroll wheel does the same thing.")]
    [SerializeField] private KeyCode rotateKey = KeyCode.X;
    [SerializeField] public static bool rotationMode = true; // Determines Rotation Mode
    private float placementRotation; // Building Placement Rotation
    private Quaternion baseRotation = Quaternion.identity;

    /// <summary>Whether the player has scrolled this blueprint, taking over its facing.</summary>
    public bool HasPlayerRotated { get; private set; }

    /// <summary>
    /// The orientation the player's rotation is measured from. Ignored once the player
    /// has rotated, so an automatic facing cannot fight a deliberate one.
    /// </summary>
    public void SetBaseRotation(Quaternion rotation)
    {
        if (HasPlayerRotated || baseRotation == rotation) return;

        baseRotation = rotation;
        Apply();
    }

    private void Update()
    {
        RotateBuilding();
    }

    private void RotateBuilding()
    {
        if (!rotationMode) return;

        // One discrete step per press, so a key held down does not spin the blueprint.
        if (Input.GetKeyDown(rotateKey))
        {
            Rotate(1);
            return;
        }

        // Rotational System -- Flat Based: Quadral 90 degrees
        float scrollDirection = Input.GetAxis("Mouse ScrollWheel") * 10.0f; // Scaling factor
        if (Mathf.Approximately(scrollDirection, 0f)) return;

        // At least one step per notch. Flooring the scaled axis on its own swallowed any
        // notch that reported less than 0.1, which is most of them on a fine-resolution
        // wheel - the blueprint simply never turned.
        int steps = Mathf.Max(1, Mathf.FloorToInt(Mathf.Abs(scrollDirection)));
        Rotate((int)Mathf.Sign(scrollDirection) * steps);
    }

    /// <summary>Turns the blueprint by whole quarter-turn steps.</summary>
    public void Rotate(int steps)
    {
        if (steps == 0) return;

        placementRotation += steps * rotationAngleAmount;
        HasPlayerRotated = true;

        Apply();
    }

    private void Apply()
    {
        transform.rotation = baseRotation * Quaternion.AngleAxis(placementRotation, Vector3.down);
    }
}
