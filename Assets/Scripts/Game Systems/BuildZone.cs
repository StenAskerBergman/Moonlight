using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuildZone : MonoBehaviour
{
    public Texture2D grayOverlayTexture;
    public Texture2D maskTexture;
    public Color maskColor = Color.white;

    private Vector3 buildingPosition;
    private bool isPlacingBuilding = false;

private void Start() {
    // Create the gray overlay texture
    grayOverlayTexture = new Texture2D(Screen.width, Screen.height);
    Color[] pixels = new Color[Screen.width * Screen.height];
    for (int i = 0; i < pixels.Length; i++) {
        pixels[i] = new Color(0.5f, 0.5f, 0.5f, 0.5f);
    }
    grayOverlayTexture.SetPixels(pixels);
    grayOverlayTexture.Apply();
}

private void Update() {
    if (isPlacingBuilding) {
        // Calculate the valid placement areas based on the building's size and position constraints
        Rect validPlacementArea = CalculateValidPlacementArea(buildingPosition);

        // Create the mask texture
        maskTexture = new Texture2D(Screen.width, Screen.height);
        for (int x = 0; x < Screen.width; x++) {
            for (int y = 0; y < Screen.height; y++) {
                if (validPlacementArea.Contains(new Vector2(x, y))) {
                    maskTexture.SetPixel(x, y, maskColor);
                } else {
                    maskTexture.SetPixel(x, y, Color.clear);
                }
            }
        }
        maskTexture.Apply();

        // Overlay the mask texture on top of the gray overlay texture
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), grayOverlayTexture);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), maskTexture);
    }
}

private void OnPlaceBuilding(Vector3 position) {
    buildingPosition = position;
    isPlacingBuilding = true;
}

private void OnMoveBuilding(Vector3 position) {
    buildingPosition = position;
}

private void OnCancelBuildingPlacement() {
    isPlacingBuilding = false;
}

private Rect CalculateValidPlacementArea(Vector3 position) {
    // Calculate the valid placement area based on the building's size and position constraints
    // ...
    return new Rect();
}

}
