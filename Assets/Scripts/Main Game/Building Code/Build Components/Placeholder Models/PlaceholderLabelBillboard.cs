using UnityEngine;

/// <summary>
/// Keeps a placeholder name plate facing the active camera, whatever way the building
/// itself was rotated on the grid. Copies the camera's rotation rather than looking at
/// its position, so every label on screen stays parallel and reads like a HUD tag.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlaceholderLabelBillboard : MonoBehaviour
{
    private void LateUpdate()
    {
        Camera camera = Camera.main;
        if (camera == null) return;

        transform.rotation = camera.transform.rotation;
    }
}
