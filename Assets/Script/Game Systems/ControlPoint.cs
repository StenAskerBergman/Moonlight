using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ControlPoint : MonoBehaviour
{
    public int teamOwned;
    public Material[] teamMaterials;
    private MeshRenderer meshRenderer;

    private void Start()
    {
        meshRenderer = GetComponent<MeshRenderer>();
    }

    public void CapturePoint(int capturingTeam)
    {
        teamOwned = capturingTeam;
        meshRenderer.material = teamMaterials[capturingTeam];
    }
}