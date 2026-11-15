using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Drives subtle operational life (spinning turbines/rotors and emissive status beacons)
/// on procedurally assembled buildings, tracking BuildingSimulation state and efficiency.
/// </summary>
[DisallowMultipleComponent]
public sealed class ProceduralBuildingLifeDriver : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private BuildingSimulation simulation;
    private readonly List<Transform> rotors = new List<Transform>();
    private readonly List<Renderer> statusLeds = new List<Renderer>();
    private MaterialPropertyBlock propertyBlock;
    private float phaseOffset;
    private float spinRate = 75f;

    private BuildingEnums.BuildingState lastState = (BuildingEnums.BuildingState)(-1);
    private float lastPulseBucket = -1f;

    private void Awake()
    {
        simulation = GetComponentInParent<BuildingSimulation>();
        phaseOffset = (Mathf.Abs(GetInstanceID()) % 97) * 0.065f;
        spinRate = 65f + (Mathf.Abs(GetInstanceID()) % 35);
        CollectParts();
    }

    public void RefreshParts()
    {
        CollectParts();
        lastState = (BuildingEnums.BuildingState)(-1);
    }

    private void CollectParts()
    {
        rotors.Clear();
        statusLeds.Clear();

        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child == null) continue;
            string n = child.name;
            if (n.IndexOf("Rotor", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                rotors.Add(child);
            }
            else if (n.IndexOf("StatusLED", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Renderer rend = child.GetComponent<Renderer>();
                if (rend != null) statusLeds.Add(rend);
            }
        }
    }

    private void Update()
    {
        if (simulation == null)
        {
            simulation = GetComponentInParent<BuildingSimulation>();
            if (simulation == null) return;
        }

        BuildingEnums.BuildingState state = simulation.CurrentState;
        float efficiency = simulation.CurrentEfficiency;

        if (state == BuildingEnums.BuildingState.Active && efficiency > 0.01f && rotors.Count > 0)
        {
            float deltaAngle = spinRate * efficiency * Time.deltaTime;
            for (int i = 0; i < rotors.Count; i++)
            {
                if (rotors[i] != null)
                {
                    rotors[i].Rotate(Vector3.up, deltaAngle, Space.Self);
                }
            }
        }

        UpdateLedVisuals(state);
    }

    private void UpdateLedVisuals(BuildingEnums.BuildingState state)
    {
        if (statusLeds.Count == 0) return;

        float pulse = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 3.2f + phaseOffset);
        float bucket = Mathf.Round(pulse * 12f);
        if (state == lastState && Mathf.Approximately(bucket, lastPulseBucket)) return;

        lastState = state;
        lastPulseBucket = bucket;

        Color baseColor;
        Color emission;

        if (simulation.CurrentShutdownReason == BuildingEnums.BuildingShutdownReason.Damaged ||
            state == BuildingEnums.BuildingState.Destroyed)
        {
            baseColor = new Color(0.95f, 0.16f, 0.14f, 1f);
            emission = baseColor * (1.3f * pulse);
        }
        else
        {
            switch (state)
            {
                case BuildingEnums.BuildingState.Active:
                    baseColor = new Color(0.18f, 0.95f, 0.45f, 1f);
                    emission = baseColor * (1.5f * pulse);
                    break;

                case BuildingEnums.BuildingState.Paused:
                case BuildingEnums.BuildingState.Inactive:
                    baseColor = new Color(0.98f, 0.58f, 0.12f, 1f);
                    emission = baseColor * (1.25f * pulse);
                    break;

                default: // UnderConstruction
                    baseColor = new Color(0.15f, 0.32f, 0.38f, 1f);
                    emission = baseColor * 0.25f;
                    break;
            }
        }

        propertyBlock ??= new MaterialPropertyBlock();
        for (int i = 0; i < statusLeds.Count; i++)
        {
            Renderer r = statusLeds[i];
            if (r == null) continue;
            r.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, baseColor);
            propertyBlock.SetColor(EmissionColorId, emission);
            r.SetPropertyBlock(propertyBlock);
        }
    }
}
