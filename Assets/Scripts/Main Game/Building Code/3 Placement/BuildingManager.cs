using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Author: Sten
// Purpose: Manage Building
public class BuildingManager : MonoBehaviour
{/*
    public Material previewMaterial; // Preview Materials
    public GameObject Building; // "Intended" Building Model
    public GameObject currentBuilding; // Current Building Model
    
    [SerializeField] private GameObject building;   // Perma Building Transform
    [SerializeField] private bool Selected;         // Building selectionality
    [SerializeField] private bool Functional;       // Building Functionality
    [SerializeField] private bool Requirements;     // Building Requirements
    [SerializeField] private bool Needs;            // Building Needs
*/
    private List<GameObject> buildings = new List<GameObject>();

    public void RegisterBuilding(GameObject building)
    {
        buildings.Add(building);
    }

    private void Update()
    {
        // Handle building interactions, menus, events, and conditions
    }
}

public class BuildingMaterials : MonoBehaviour
{
    public List<Material> originalMaterials;
}

// Old Building Manager Code

    // public void SetBuildingPrefab(GameObject newBuildingPrefab)
    // {
    //     // Set the preview object with the new building prefab
    //     Building = newBuildingPrefab;
    // }

    // Preview Mode Method: Determins Materials
    // public void SetPreviewMode(GameObject building, bool isPreview)
    // {
    //     if (isPreview)
    //     {
    //         ApplyPreviewMaterial(building);
    //         DisableColliders(building);
    //     }
    //     else
    //     {
    //         RestoreOriginalMaterials(building);
    //         EnableColliders(building);
    //     }
    // }


    // private void ApplyPreviewMaterial(GameObject building)
    // {
    //     MeshRenderer[] meshRenderers = building.GetComponentsInChildren<MeshRenderer>();
    //     foreach (MeshRenderer meshRenderer in meshRenderers)
    //     {
    //         // Store the original materials in a list
    //         List<Material> originalMaterials = new List<Material>(meshRenderer.materials);

    //         // Store the original materials in the MeshRenderer's GameObject as a reference
    //         meshRenderer.gameObject.AddComponent<BuildingMaterials>().originalMaterials = originalMaterials;

    //         // Apply the preview material
    //         meshRenderer.material = previewMaterial;
    //     }
    // }

    // private void RestoreOriginalMaterials(GameObject building)
    // {
    //     MeshRenderer[] meshRenderers = building.GetComponentsInChildren<MeshRenderer>();
    //     foreach (MeshRenderer meshRenderer in meshRenderers)
    //     {
    //         BuildingMaterials materialReference = meshRenderer.gameObject.GetComponent<BuildingMaterials>();
    //         if (materialReference != null)
    //         {
    //             // Restore the original materials
    //             meshRenderer.materials = materialReference.originalMaterials.ToArray();

    //             // Remove the BuildingMaterials component
    //             Destroy(materialReference);
    //         }
    //     }
    // }

    // private void DisableColliders(GameObject building)
    // {
    //     Collider[] colliders = building.GetComponentsInChildren<Collider>();
    //     foreach (Collider collider in colliders)
    //     {
    //         collider.enabled = false;
    //     }
    // }

    // private void EnableColliders(GameObject building)
    // {
    //     Collider[] colliders = building.GetComponentsInChildren<Collider>();
    //     foreach (Collider collider in colliders)
    //     {
    //         collider.enabled = true;
    //     }
    // }

    
    //private Dictionary<Island, List<Building>> buildings = new Dictionary<Island, List<Building>>();

    // public void AddBuilding(Island island, Building building)
    // {
    //     if (!buildings.ContainsKey(island))
    //     {
    //         buildings[island] = new List<Building>();
    //     }

    //     buildings[island].Add(building);
    //     //CalculateMonthlyReturns(island);
    // }

    // private void CalculateMonthlyReturns(Island island)
    // {
    //     int monthlyReturns = 0;
    //     foreach (Building building in buildings[island])
    //     {
    //         monthlyReturns += building.MonthlyReturns;
    //     }

    //     // Give monthly returns to player for this island
    //     ResourceManager.Instance.AddResourceToIsland(island.Id, Enums.Resource.Money, monthlyReturns);
    // }

    // public void RemoveBuilding(Building building)
    // {
    //     foreach (var kvp in buildings)
    //     {
    //         Island island = kvp.Key;
    //         List<Building> islandBuildings = kvp.Value;

    //         if (islandBuildings.Contains(building))
    //         {
    //             islandBuildings.Remove(building);
    //             //CalculateMonthlyReturns(island);
    //             break;
    //         }
    //     }
    // }


    // public int GetBuildingCount()
    // {
    //     return buildings.Count;
    // }
