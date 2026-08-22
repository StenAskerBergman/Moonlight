using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuildingEffect : MonoBehaviour
{
    private Building building;

    // TODO: assign per-prefab in the Inspector - not every building needs both.
    [SerializeField] private ParticleSystem activeVFX;
    [SerializeField] private ParticleSystem constructionVFX;

    // TODO: assign per-prefab in the Inspector - point at the building's visible mesh renderers.
    [SerializeField] private Renderer[] buildingRenderers;
    [SerializeField] private Material activeMaterial;
    [SerializeField] private Material inactiveMaterial;
    [SerializeField] private Material constructionMaterial;

    private void Awake()
    {
        building = GetComponent<Building>();
    }

    private void OnEnable()
    {
        Building.OnBuildingStateChanged += HandleBuildingStateChanged;
    }

    private void OnDisable()
    {
        Building.OnBuildingStateChanged -= HandleBuildingStateChanged;
    }

    private void HandleBuildingStateChanged(Building changedBuilding, BuildingEnums.BuildingState newState)
    {
        if (changedBuilding != building) return;

        switch (newState)
        {
            case BuildingEnums.BuildingState.UnderConstruction:
                StopVFX(activeVFX);
                PlayVFX(constructionVFX);
                SwapMaterial(constructionMaterial);
                break;

            case BuildingEnums.BuildingState.Active:
                StopVFX(constructionVFX);
                PlayVFX(activeVFX);
                SwapMaterial(activeMaterial);
                break;

            case BuildingEnums.BuildingState.Inactive:
            case BuildingEnums.BuildingState.Paused:
                StopVFX(activeVFX);
                SwapMaterial(inactiveMaterial);
                break;

            case BuildingEnums.BuildingState.Destroyed:
                StopVFX(activeVFX);
                StopVFX(constructionVFX);
                // Renderers are left as-is; BuildingDestroyer handles GameObject removal.
                break;
        }
    }

    private void PlayVFX(ParticleSystem vfx)
    {
        if (vfx == null) return;
        vfx.Play();
    }

    private void StopVFX(ParticleSystem vfx)
    {
        if (vfx == null) return;
        vfx.Stop();
    }

    private void SwapMaterial(Material material)
    {
        if (material == null || buildingRenderers == null) return;

        foreach (Renderer buildingRenderer in buildingRenderers)
        {
            if (buildingRenderer == null) continue;
            buildingRenderer.material = material;
        }
    }
}
