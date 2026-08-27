using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Authoritative simulation component for persistent ruins / rubble left after combat or catastrophic destruction.
/// Keeps footprint cells blocked on the GridSystem until cleared by a player clearing task.
/// </summary>
public class RuinSite : MonoBehaviour
{
    [Header("Ruin Configuration")]
    [SerializeField] private Vector3 buildingSize = Vector3.one;
    [SerializeField] private int clearingCost = 50;

    [Header("Deliverable Assets (Optional)")]
    [SerializeField] private GameObject rubbleModel;
    [SerializeField] private ParticleSystem lingeringSmokeVfx;
    [SerializeField] private AudioClip clearRubbleSound;

    private GridSystem _gridSystem;
    private List<Vector2Int> _occupiedCellIndices = new List<Vector2Int>();

    public int ClearingCost => clearingCost;

    public void Initialize(GridSystem grid, Vector3 size, List<Vector2Int> cells)
    {
        _gridSystem = grid;
        buildingSize = size;
        if (cells != null)
        {
            _occupiedCellIndices = new List<Vector2Int>(cells);
        }

        if (rubbleModel == null)
        {
            AssetFallback.LogMissingDeliverable("GameObject", "rubbleModel", this);
        }
        if (lingeringSmokeVfx == null)
        {
            AssetFallback.LogMissingDeliverable("ParticleSystem", "lingeringSmokeVfx", this);
        }
    }

    /// <summary>
    /// Clears the rubble, frees the occupied cells on the GridSystem, and destroys the ruin object.
    /// </summary>
    public void ClearRuin()
    {
        if (_gridSystem != null && _occupiedCellIndices != null)
        {
            foreach (Vector2Int cellIdx in _occupiedCellIndices)
            {
                Cell cell = _gridSystem.GetCell(cellIdx.x, cellIdx.y);
                cell?.ReleaseCell();
            }
        }

        if (clearRubbleSound != null)
        {
            AudioSource.PlayClipAtPoint(clearRubbleSound, transform.position);
        }
        else
        {
            AssetFallback.LogMissingDeliverable("AudioClip", "clearRubbleSound", this);
        }

        Destroy(gameObject);
    }
}
