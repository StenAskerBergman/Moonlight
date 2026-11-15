using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class BuildingProperties : MonoBehaviour
{    
    #region Building Variables

	// Island & Grid Refs
        public Island currentIsland;
		public GridSystem gridSystem;
    
    // Building Properties
		public BuildingProperties buildingProperties;
    
    // Building Data
        public BuildingData buildingData;
        public CostData costData;
        
    // Building Settings & Variables
        public Vector3 buildingSize;
        public float influenceRadius = 0f;
        public string buildingName;
        public string buildingDescription;
        public string[] buildingTags;

    // Building Model Components
    //public Renderer buildingRenderer;

    // Ideas:
    // Make this a string[] and hold all the names for this building,
    // remember that different player see different names depending
    // on a lot of things! Being enemies, allies, under attack vice
    // versa. Therefore there needs to be a generative method to fix
    // the different names for the different views.

    // future issue:
    // There also needs to be a way for preview buildings not to be
    // viewed by other players

    // future feature:
    // we need a way to control the way we show + hold building data,
    // from players in the future in each building to be read so that
    // it is read correctly but also so that depending on each player
    // and their intelligence levels / insight into a another player,
    // is show the correct data, so that in the future players cannot
    // do object manipulate to see this data inorder to cheat / edge.
    // 
    // Real Reason: plus it would be cool to have. :spy!: :poggers:

    #endregion

    private void Start()
    {
        SetData();
    }

    /// <summary>
    /// The footprint this building will actually have.
    ///
    /// <see cref="SetData"/> lets BuildingData override the prefab's serialized size at
    /// runtime, but it only runs in Start - so anything reading buildingSize before that
    /// (the blueprint, and BuildingPlacer on the frame of the click) saw a stale value.
    /// City Center's prefab says 0x0 while its BuildingData says 6x8, which silently
    /// reduced it to a single cell. Both now resolve the size the same way.
    /// </summary>
    public static Vector3 ResolveSize(BuildingProperties properties, BuildingData data)
    {
        if (data != null && data.buildingSize != Vector3.zero) return data.buildingSize;
        if (properties != null && properties.buildingSize != Vector3.zero) return properties.buildingSize;

        return Vector3.one;
    }

    /// <summary>
    /// Resolves the candidate influence/territory radius for a building.
    /// Checks BuildingProperties, BuildingData, InfluenceZone component, or uses a default.
    /// </summary>
    public static float ResolveInfluenceRadius(BuildingProperties properties, BuildingData data, float fallback = 0f)
    {
        if (data != null && data.influenceRadius > 0f) return data.influenceRadius;
        if (properties != null && properties.influenceRadius > 0f) return properties.influenceRadius;

        if (properties != null)
        {
            InfluenceZone zone = properties.GetComponent<InfluenceZone>();
            if (zone != null && zone.Radius > 0f) return zone.Radius;

            // Harbor / Depot / Warehouse / City Center fallback influence if not configured
            if (InfluenceManager.IsHarborBuilding(properties)) return 24f;
        }

        return fallback;
    }

    public void SetData()
    {
        /// Edit: In the future include, what replacement was added in its place!
        // Legacy Code 
        // Fetch Resource Cost Data
        // int[] costResources = costData.costResource;

        // Fetch Size Data. A missing BuildingData asset is normal - the Depot has none -
        // and simply means there is nothing to override the size authored on the prefab.
        // Reading through it unguarded threw in Start() on every building placed from
        // such a prefab.
        if (buildingData != null)
        {
            buildingSize = buildingData.buildingSize;
            if (buildingData.influenceRadius > 0f)
            {
                influenceRadius = buildingData.influenceRadius;
            }
        }
    }


    // Update the Initialize method to take currentIsland and gridSystem as parameters
    public void Initialize(Island island, GridSystem detectedGridSystem)
    {
        currentIsland = island;
        gridSystem = detectedGridSystem;
    }

    /// <summary>
    /// Computes the integer grid footprint (in cells) accounting for 90-degree Y rotations.
    /// </summary>
    public static Vector2Int ResolveFootprint(BuildingProperties properties, BuildingData data, Quaternion rotation)
    {
        Vector3 size = ResolveSize(properties, data);
        int width = Mathf.Max(1, Mathf.RoundToInt(size.x));
        int length = Mathf.Max(1, Mathf.RoundToInt(size.z));

        int quarterTurns = Mathf.RoundToInt(rotation.eulerAngles.y / 90f);
        if ((quarterTurns & 1) != 0)
        {
            return new Vector2Int(length, width);
        }

        return new Vector2Int(width, length);
    }

    /// <summary>
    /// Resolves this placed building's island grid, footprint origin cell, and rotated cell dimensions.
    /// </summary>
    public bool TryGetPlacementCells(out GridSystem grid, out Vector3Int origin, out Vector2Int footprint)
    {
        grid = gridSystem;
        if (grid == null)
        {
            Island island = currentIsland != null ? currentIsland : GetComponentInParent<Island>();
            if (island != null)
            {
                grid = island.GetComponent<GridSystem>();
            }
        }

        if (grid == null)
        {
            origin = Vector3Int.zero;
            footprint = Vector2Int.one;
            return false;
        }

        footprint = ResolveFootprint(this, buildingData, transform.rotation);
        origin = grid.GetFootprintOrigin(transform.position, footprint);
        return true;
    }
}