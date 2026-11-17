// tldr: Owns the MAP: picks the pattern, lays out the chunk lattice, spawns one chunk prefab per occupied
// slot, hands each chunk the world-level context (ocean datum + plateau profile), and validates that
// the finished chunks tile without overlapping. It does NOT generate terrain - it only decides where
// chunks go and what world contract they generate against.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UIElements;

/*  File Role: Creating the map of the game 

    Author: Sten

    The MapManager script is responsible for creating 
    the map of the game. It creates the islands, sets 
    their positions, and creates the borders around them. 
    It also handles clicking on an island to select it 
    and displaying the name and resources of the island 
    in the UI.

*/

// MapManager
// tldr: responsible for spawning in empty prefab islands objects

// MapGrid
// tldr: responsible for actual generation of the island on awake

public class MapManager : MonoBehaviour
{
    public static MapManager instance { get; private set; }
    private const string GeneratedMapRootName = "Generated Map";

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        instance = this;
        ValidateAndMigratePatternData();

        if (FindObjectOfType<PlayerSpawnManager>() == null)
        {
            gameObject.AddComponent<PlayerSpawnManager>();
        }
    }

    #region Map Variables

    public enum SpawnCondition
    {
        All,
        SelectedSlotIds,
        ExcludingSlotIds,
        Border,
        Interior
    }

    [Serializable]
    public sealed class SpawnRule
    {
        public string name = "New Rule";
        [Tooltip("Rules are evaluated from top to bottom. The first match wins.")]
        public SpawnCondition condition = SpawnCondition.All;
        [Tooltip("Used by Selected Slot IDs and Excluding Slot IDs conditions.")]
        public List<int> slotIds = new List<int>();
        public bool invertCondition;

        [Header("Result")]
        public GridType.Type terrainType = GridType.Type.Island;
        [Tooltip("Optional prefab for matching slots. Leave empty to use the pattern's chunk prefab.")]
        public GameObject prefabOverride;

        [Tooltip("Give matching slots their own deviations from the shared world terrain contract. " +
                 "Authored here rather than on a spawned chunk because chunks are destroyed and " +
                 "rebuilt from the prefab on every regeneration - an override set on a live island " +
                 "would not survive one.")]
        public bool applyGenerationOverrides;
        public IslandGenerationOverrides generationOverrides = new IslandGenerationOverrides();

        public bool Matches(int slotId, int row, int column, int gridSize)
        {
            bool matches;
            switch (condition)
            {
                case SpawnCondition.SelectedSlotIds:
                    matches = slotIds != null && slotIds.Contains(slotId);
                    break;
                case SpawnCondition.ExcludingSlotIds:
                    matches = slotIds == null || !slotIds.Contains(slotId);
                    break;
                case SpawnCondition.Border:
                    matches = gridSize <= 1 || row == 0 || column == 0
                              || row == gridSize - 1 || column == gridSize - 1;
                    break;
                case SpawnCondition.Interior:
                    matches = gridSize > 2 && row > 0 && column > 0
                              && row < gridSize - 1 && column < gridSize - 1;
                    break;
                default:
                    matches = true;
                    break;
            }

            return invertCondition ? !matches : matches;
        }
    }

    // One PatternData item is the complete interface for a map-generation run.
    [Serializable]
    public sealed class PatternData
    {
        private const int CurrentDataVersion = 6;

        public string displayName = "New Pattern";
        [FormerlySerializedAs("patternName")]
        [SerializeField, HideInInspector] private string legacyPatternName;
        [SerializeField, HideInInspector] private int dataVersion;

        [Header("Pattern Identification")]
        [FormerlySerializedAs("patternDescription")]
        [TextArea(2, 3)]
        public string description;
        public SpawnPattern spawnPattern = SpawnPattern.Normal;

        [Header("Grid Layout")]
        [Tooltip("Number of lattice slots along each axis (e.g. 7 for 7x7 grid)")]
        [Range(0, 49)]
        [FormerlySerializedAs("numberOfIslands")]
        public int gridSize = 7;
        [Tooltip("Spacing pitch between adjacent lattice slots")]
        [FormerlySerializedAs("islandSpacing")]
        public float slotSpacing = 30f;

        [Header("Selection & Masking")]
        [Tooltip("Invert the default-terrain slot selection for this pattern.")]
        public bool invertSelection;
        [Tooltip("Slots that receive the default terrain. Inverted selection generates the default terrain everywhere except these slots.")]
        public List<int> currentIslandSelection = new List<int>();
        [Tooltip("Additional underwater slots used by the normal world pattern. These resolve to the terrain type below when selection is not inverted.")]
        public List<int> currentOceanSelection = new List<int>();
        [Tooltip("Terrain generated in the underwater slot list. Plateau preserves the original deep-sea plateau pattern; Ocean is available for intentionally flat seabed chunks.")]
        public GridType.Type underwaterSelectionTerrainType = GridType.Type.Plateau;

        [Header("Chunk Generation")]
        [FormerlySerializedAs("islandConfig")]
        [Tooltip("Island archetype/resource configuration used by every generated chunk in this pattern.")]
        public IslandConfiguration configuration;
        [Tooltip("Inline plateau geometry configuration used when this pattern resolves a Plateau slot.")]
        public StandalonePlateauSettings plateauSettings = new StandalonePlateauSettings();
        public GridType.Type defaultTerrainType = GridType.Type.Island;
        [FormerlySerializedAs("islandPrefab")]
        [Tooltip("Required prefab used unless the first matching slot override supplies another prefab.")]
        public GameObject defaultChunkPrefab;

        [Header("Ordered Slot Overrides")]
        [FormerlySerializedAs("terrainRules")]
        [Tooltip("Evaluated from top to bottom. The first match supplies terrain and may replace the chunk prefab.")]
        public List<SpawnRule> slotOverrides = new List<SpawnRule>();

        [Header("Water Settings")]
        public bool waterOnStart = true;
        public float waterHeight;

        // Migration-only state. Runtime generation never reads these switches or
        // redirects authority to MapManager's old scene-level defaults.
        [FormerlySerializedAs("overrideSelectionMasks")]
        [SerializeField, HideInInspector] private bool legacyOverrideSelectionMasks;
        [FormerlySerializedAs("useTerrainRules")]
        [SerializeField, HideInInspector] private bool legacyUseTerrainRules;
        [FormerlySerializedAs("overrideWaterSettings")]
        [SerializeField, HideInInspector] private bool legacyOverrideWaterSettings;
        [FormerlySerializedAs("oceanTilePrefab")]
        [SerializeField, HideInInspector] private GameObject legacyOceanTilePrefab;

        public PatternData()
        {
            currentIslandSelection = new List<int>();
            currentOceanSelection = new List<int>();
            slotOverrides = new List<SpawnRule>();
        }

        public SpawnRule FindMatchingRule(int slotId, int row, int column, int gridSize)
        {
            if (slotOverrides == null) return null;

            foreach (SpawnRule rule in slotOverrides)
            {
                if (rule != null && rule.Matches(slotId, row, column, gridSize)) return rule;
            }

            return null;
        }

        public bool TryResolveSlot(
            int slotId,
            int row,
            int column,
            out GridType.Type terrainType,
            out GameObject chunkPrefab,
            out IslandGenerationOverrides generationOverrides)
        {
            generationOverrides = null;

            SpawnRule rule = FindMatchingRule(slotId, row, column, gridSize);
            if (rule != null)
            {
                terrainType = rule.terrainType;
                chunkPrefab = rule.prefabOverride != null ? rule.prefabOverride : defaultChunkPrefab;
                if (rule.applyGenerationOverrides) generationOverrides = rule.generationOverrides;
                return terrainType != GridType.Type.Empty;
            }

            bool selectedForDefault = spawnPattern == SpawnPattern.Singular
                || (invertSelection
                    ? !currentIslandSelection.Contains(slotId)
                    : currentIslandSelection.Contains(slotId));
            if (selectedForDefault)
            {
                terrainType = defaultTerrainType;
                chunkPrefab = defaultChunkPrefab;
                return terrainType != GridType.Type.Empty;
            }

            if (!invertSelection && currentOceanSelection.Contains(slotId))
            {
                terrainType = underwaterSelectionTerrainType;
                chunkPrefab = defaultChunkPrefab;
                return terrainType != GridType.Type.Empty;
            }

            terrainType = GridType.Type.Empty;
            chunkPrefab = null;
            return false;
        }

        public void ValidateAndMigrate(
            int index,
            int legacyGridSize,
            float legacySlotSpacing,
            List<int> legacyIslandSelection,
            List<int> legacyOceanSelection,
            IslandConfiguration legacyConfiguration,
            GameObject legacyChunkPrefab,
            bool legacyWaterOnStart,
            float legacyWaterHeight)
        {
            if (dataVersion < 1 && !string.IsNullOrWhiteSpace(legacyPatternName))
            {
                if (string.IsNullOrWhiteSpace(displayName)) displayName = legacyPatternName;

                // Entries serialized before terrain assignment existed represented
                // ordinary islands. Preserve that meaning instead of interpreting the
                // enum's zero value as Plateau during schema migration.
                defaultTerrainType = GridType.Type.Island;
            }

            if (dataVersion < CurrentDataVersion)
            {
                gridSize = gridSize > 0 ? gridSize : Mathf.Max(1, legacyGridSize);
                slotSpacing = slotSpacing > 0f ? slotSpacing : Mathf.Max(1f, legacySlotSpacing);

                // Version 5 briefly doubled the established normal-world pitch. The
                // selection already occupies every other slot, so that change moved
                // 60-unit chunks 120 units apart and left a full ungenerated gap.
                // Restore the authored 30-unit lattice: selected neighbours then sit
                // exactly one 60-unit chunk width apart and can share their edge.
                if (dataVersion < 6
                    && spawnPattern == SpawnPattern.Normal
                    && Mathf.Approximately(slotSpacing, 60f))
                {
                    slotSpacing = 30f;
                }

                if (!legacyOverrideSelectionMasks)
                {
                    if (currentIslandSelection == null || currentIslandSelection.Count == 0)
                    {
                        currentIslandSelection = legacyIslandSelection != null
                            ? new List<int>(legacyIslandSelection)
                            : new List<int>();
                    }

                    if (currentOceanSelection == null || currentOceanSelection.Count == 0)
                    {
                        currentOceanSelection = legacyOceanSelection != null
                            ? new List<int>(legacyOceanSelection)
                            : new List<int>();
                    }
                }

                if (configuration == null) configuration = legacyConfiguration;
                if (defaultChunkPrefab == null) defaultChunkPrefab = legacyChunkPrefab;
                if (!legacyOverrideWaterSettings)
                {
                    waterOnStart = legacyWaterOnStart;
                    waterHeight = legacyWaterHeight;
                }
            }

            // Repair references independently of the schema version. Some scenes were
            // opened while this migration was still being developed, so their entries
            // were stamped with the current version before the legacy manager-level
            // references had been copied into each PatternData entry. Version-gating
            // this repair leaves those scenes permanently unable to generate.
            if (configuration == null) configuration = legacyConfiguration;
            if (defaultChunkPrefab == null) defaultChunkPrefab = legacyChunkPrefab;

            if (!Enum.IsDefined(typeof(SpawnPattern), spawnPattern)) spawnPattern = SpawnPattern.Normal;
            displayName = string.IsNullOrWhiteSpace(displayName) ? $"Pattern {index + 1}" : displayName.Trim();
            gridSize = Mathf.Clamp(gridSize, 1, 49);
            slotSpacing = Mathf.Max(1f, slotSpacing);
            currentIslandSelection ??= new List<int>();
            currentOceanSelection ??= new List<int>();
            if (dataVersion < 4)
            {
                RepairDuplicatedLegacySelectionTail(currentIslandSelection);
            }
            RepairMissingEstablishedUnderwaterSlot(currentOceanSelection, spawnPattern);
            NormalizeSlotIds(currentIslandSelection, gridSize * gridSize);
            NormalizeSlotIds(currentOceanSelection, gridSize * gridSize);
            if (underwaterSelectionTerrainType != GridType.Type.Plateau
                && underwaterSelectionTerrainType != GridType.Type.Ocean)
            {
                underwaterSelectionTerrainType = GridType.Type.Plateau;
            }
            slotOverrides ??= new List<SpawnRule>();
            foreach (SpawnRule rule in slotOverrides)
            {
                if (rule == null) continue;
                rule.slotIds ??= new List<int>();
                NormalizeSlotIds(rule.slotIds, gridSize * gridSize);
            }
            plateauSettings ??= new StandalonePlateauSettings();
            plateauSettings.Validate();
            dataVersion = CurrentDataVersion;
        }

        private static void NormalizeSlotIds(List<int> slotIds, int maximumSlotId)
        {
            HashSet<int> seen = new HashSet<int>();
            int writeIndex = 0;
            for (int readIndex = 0; readIndex < slotIds.Count; readIndex++)
            {
                int slotId = slotIds[readIndex];
                if (slotId < 1 || slotId > maximumSlotId || !seen.Add(slotId)) continue;
                slotIds[writeIndex++] = slotId;
            }

            if (writeIndex < slotIds.Count)
            {
                slotIds.RemoveRange(writeIndex, slotIds.Count - writeIndex);
            }
        }

        private static void RepairDuplicatedLegacySelectionTail(List<int> slotIds)
        {
            // A previous list migration appended slot 1 repeatedly after this exact
            // established eight-slot normal-world pattern. Earlier normalization
            // collapsed the repeats but could not know that the remaining ninth id
            // was migration debris. Recognise the original prefix once and remove
            // only its appended tail; arbitrary custom selections are untouched.
            int[] establishedSlots = { 3, 17, 19, 21, 29, 31, 33, 47 };
            if (slotIds.Count <= establishedSlots.Length) return;

            for (int index = 0; index < establishedSlots.Length; index++)
            {
                if (slotIds[index] != establishedSlots[index]) return;
            }

            slotIds.RemoveRange(
                establishedSlots.Length,
                slotIds.Count - establishedSlots.Length);
        }

        private static void RepairMissingEstablishedUnderwaterSlot(
            List<int> slotIds,
            SpawnPattern pattern)
        {
            if (pattern != SpawnPattern.Normal || slotIds.Count != 7) return;

            int[] survivingSlots = { 5, 7, 15, 35, 43, 45, 49 };
            for (int index = 0; index < survivingSlots.Length; index++)
            {
                if (slotIds[index] != survivingSlots[index]) return;
            }

            // Slot 1 belongs to the authored eight-plateau normal-world mask. It
            // disappeared during the interrupted list migration that also produced
            // the duplicated island tail; recognise only that exact seven-slot
            // remainder so arbitrary custom masks are never expanded.
            slotIds.Insert(0, 1);
        }
    }


    public enum SpawnPattern
    {
        Singular,
        Circlar,
        Square,
        Normal,
    }

    public static event Action OnMapGenerated;
    public static event Action<MapManager, string> OnMapGenerationFailed;

    [HideInInspector] public long LastGenerationTimeMs = -1;
    [HideInInspector] public string LastGenerationBreakStatus = null;

    /// <summary>
    /// Chunks that threw during the last GenerateMap() and were dropped. Zero is the
    /// only healthy value - anything else means the generated world is missing slots.
    /// </summary>
    [HideInInspector] public int LastGenerationFailedChunks = 0;
    private int failedChunkCount;
    private int chunksStartedThisRun;
    [Header("Generation Watchdog Guard")]
    [Tooltip("Maximum allowed generation time in seconds before auto-breaking")]
    [SerializeField] private float generationTimeoutSeconds = 15f;
    public float GenerationTimeoutSeconds => generationTimeoutSeconds;

    public bool IsSelectionInverted
    {
        get
        {
            PatternData selectedPattern = SelectedPatternData;
            return selectedPattern != null && selectedPattern.invertSelection;
        }
    }

    public PatternData SelectedPatternData
    {
        get
        {
            if (patternDataList == null)
            {
                return null;
            }

            if (selectedPatternDataIndex >= 0 && selectedPatternDataIndex < patternDataList.Count)
            {
                return patternDataList[selectedPatternDataIndex];
            }

            return null;
        }
    }

    public List<int> ActiveIslandSelection
    {
        get
        {
            PatternData activePattern = SelectedPatternData;
            return activePattern?.currentIslandSelection ?? EmptySlotSelection;
        }
    }

    public List<int> ActiveOceanSelection
    {
        get
        {
            PatternData activePattern = SelectedPatternData;
            return activePattern?.currentOceanSelection ?? EmptySlotSelection;
        }
    }

    public GridType.Type ActiveUnderwaterSelectionTerrainType
    {
        get
        {
            PatternData activePattern = SelectedPatternData;
            return activePattern?.underwaterSelectionTerrainType ?? GridType.Type.Plateau;
        }
    }

    public IslandConfiguration ActiveConfiguration
    {
        get
        {
            PatternData activePattern = SelectedPatternData;
            return activePattern?.configuration;
        }
    }

    public GameObject ActiveChunkPrefab
    {
        get
        {
            PatternData activePattern = SelectedPatternData;
            return activePattern?.defaultChunkPrefab;
        }
    }

    [Header("Spawn Patterns")]
    [Tooltip("Collection of configured map generation patterns.")]
    public List<PatternData> patternDataList;
    [FormerlySerializedAs("selectedSpawnPattern")]
    [SerializeField, HideInInspector] private SpawnPattern legacySelectedSpawnPattern;
    [SerializeField, HideInInspector] private int selectedPatternDataIndex = -1;
    private static readonly List<int> EmptySlotSelection = new List<int>();

    public int SelectedPatternDataIndex => selectedPatternDataIndex;

    public void SelectPatternData(int index)
    {
        if (patternDataList == null || index < 0 || index >= patternDataList.Count)
        {
            selectedPatternDataIndex = -1;
            return;
        }

        selectedPatternDataIndex = index;
    }

    private void OnValidate()
    {
        ValidateAndMigratePatternData();
    }

    private void ValidateAndMigratePatternData()
    {
        patternDataList ??= new List<PatternData>();
        if (patternDataList != null)
        {
            for (int i = 0; i < patternDataList.Count; i++)
            {
                PatternData pattern = patternDataList[i];
                if (pattern != null)
                {
                    pattern.ValidateAndMigrate(
                        i,
                        legacyNumberOfIslands,
                        legacyIslandSpacing,
                        legacyIslandSelection,
                        legacyOceanSelection,
                        legacyIslandConfiguration,
                        legacyIslandPrefab,
                        legacyWaterOnStart,
                        legacyWaterHeight);
                }
            }
        }

        if (selectedPatternDataIndex < 0 || selectedPatternDataIndex >= patternDataList.Count
            || patternDataList[selectedPatternDataIndex] == null)
        {
            int migratedIndex = patternDataList.FindIndex(
                pattern => pattern != null && pattern.spawnPattern == legacySelectedSpawnPattern);
            selectedPatternDataIndex = migratedIndex >= 0
                ? migratedIndex
                : patternDataList.FindIndex(pattern => pattern != null);
        }
    }


    // Prefabs ... (and below the rest of your variables)
    [FormerlySerializedAs("islandPrefab")]
    [SerializeField, HideInInspector] private GameObject legacyIslandPrefab;
    [FormerlySerializedAs("oceanTilePrefab")]
    [SerializeField, HideInInspector] private GameObject legacyOceanTilePrefab;

    public GameObject landTilePrefab => ActiveChunkPrefab;
    [SerializeField] private GameObject waterObject; // Assuming that waterObject is a reference to the water game object
    [Tooltip("The visible ocean: a tessellated surface whose waves roll in toward every island's " +
             "coast, built over the water object after each generation. The water object keeps " +
             "its collider and stays the sea-level datum but stops drawing.")]
    [SerializeField] private OceanSurfaceSettings oceanSurface = new OceanSurfaceSettings();
    [FormerlySerializedAs("islandConfig")]
    [SerializeField, HideInInspector] private IslandConfiguration legacyIslandConfiguration;

    //[Range(0, 7)] // Add Later
    [FormerlySerializedAs("numberOfIslands")]
    [SerializeField, HideInInspector] private int legacyNumberOfIslands;
    public List<Island> islands { get; private set; }
    private int nextIslandID;
    private GameManager gameManager;
    [SerializeField, HideInInspector] private Transform generatedMapRoot;
    [Space]
    [FormerlySerializedAs("WaterOnStart")]
    [SerializeField, HideInInspector] private bool legacyWaterOnStart;
    [FormerlySerializedAs("waterHeight")]
    [SerializeField, HideInInspector] private float legacyWaterHeight;

    [Space]


    [Space]
    [Header("Square Patterns Only")]
    // The LATTICE SLOT pitch, not the chunk width. See LatticeSlotSpacing.
    [FormerlySerializedAs("islandSpacing")]
    [SerializeField, HideInInspector] private float legacyIslandSpacing = 20f;
    [SerializeField] private int IslandHeight;

    // ---------------------------------------------------------------------------
    // SPATIAL CONTRACT - the numbers here are not interchangeable, and conflating
    // them is what produced both the "overlapping chunks" and the "60-unit gaps".
    //
    //   gridResolution          = 60 cells per axis        (MapGrid.size)
    //   cellWorldSize           = 1 world unit             (GridSystem.cellSize)
    //   chunkWorldSize          = 60 world units           = resolution * cellWorldSize
    //   latticeSlotSpacing      = 30 world units           (islandSpacing)
    //   occupied-slot stride    = 2                        (selection fills every other slot)
    //   generated-neighbour pitch = 60                     = stride * latticeSlotSpacing
    //
    // Chunks therefore tile exactly because
    //     occupied-slot stride * latticeSlotSpacing == chunkWorldSize.
    // ValidateGeneratedChunkSeparation catches custom selections that violate this.
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Pitch between adjacent lattice SLOTS. Half a chunk with the current sparse
    /// selection - it is not the chunk's world width.
    /// </summary>
    public int RunGridSize
    {
        get
        {
            return SelectedPatternData?.gridSize ?? 1;
        }
    }

    public float LatticeSlotSpacing
    {
        get
        {
            return SelectedPatternData?.slotSpacing ?? 1f;
        }
    }

    /// <summary>
    /// World-space width of one generated chunk: gridResolution * cellWorldSize, read
    /// from the island prefab's MapGrid.size (cells are 1 world unit, per
    /// GridSystem.cellSize). This is the extent of the terrain mesh and of
    /// Island.bounds - it is deliberately independent of LatticeSlotSpacing.
    /// </summary>
    public float ChunkWorldSize
    {
        get
        {
            GameObject prefab = ActiveChunkPrefab;
            if (prefab != null)
            {
                MapGrid prefabGrid = prefab.GetComponent<MapGrid>();
                if (prefabGrid != null && prefabGrid.Size > 0) return prefabGrid.Size;
            }

            return LatticeSlotSpacing;
        }
    }

    /// <summary>
    /// Distance subtracted from every lattice slot position so the lattice's SWEPT MESH
    /// EXTENT is centred on the world origin.
    ///
    /// The chunk transform is the mesh's MINIMUM corner, so before offsetting the
    /// lattice sweeps [0, (slots-1)*latticeSlotSpacing + chunkWorldSize] - the extra
    /// chunkWorldSize being the last chunk's own body. Centring the slot POSITIONS
    /// instead of that swept extent left the terrain half a chunk positive: the map
    /// read [-90, 150] rather than [-120, 120].
    ///
    /// This generalises what the Singular pattern already hardcodes - substituting
    /// slots = 1 yields exactly its chunkWorldSize/2.
    /// </summary>
    public float LatticeCenteringOffset
    {
        get
        {
            int slots = Mathf.Max(1, RunGridSize);
            return ((slots - 1) * LatticeSlotSpacing + ChunkWorldSize) * 0.5f;
        }
    }

    /// <summary>
    /// Bounds for a chunk whose MINIMUM CORNER is at minCorner. The MapGrid transform
    /// sits on that corner and its mesh spans local 0..ChunkWorldSize, so the bounds
    /// centre is half a chunk further along X and Z. Keeping this in one place is what
    /// makes Island.bounds actually enclose the terrain it claims to.
    /// </summary>
    private Bounds MakeChunkBounds(Vector3 minCorner)
    {
        float width = ChunkWorldSize;
        return new Bounds(
            minCorner + new Vector3(width * 0.5f, 0f, width * 0.5f),
            new Vector3(width, width, width));
    }

    // Calculate the offset needed to start the islands in the center of the scene
    public float xOffset { get; private set; }
    public float zOffset { get; private set; }

    [FormerlySerializedAs("invertSelection")]
    [SerializeField, HideInInspector] private bool legacyInvertSelection;
    [FormerlySerializedAs("currentIslandSelection")]
    [SerializeField, HideInInspector] private List<int> legacyIslandSelection;
    [FormerlySerializedAs("currentOceanSelection")]
    [SerializeField, HideInInspector] private List<int> legacyOceanSelection;

    #endregion

    #region Match Configuration

    /// <summary>
    /// Overrides this map's generation inputs with the lobby's choices.
    ///
    /// Must be called before Start(), which is where generation actually happens -
    /// MatchBootstrapper does this from Awake(). islandPrefab is deliberately not
    /// overridable: it is scene wiring, not a match setting.
    /// </summary>
    public void ApplyConfig(MatchConfig config)
    {
        if (config == null)
        {
            return;
        }

        if (islands != null)
        {
            Debug.LogWarning("MapManager: ApplyConfig ran after generation - the map " +
                             "is already built and these values will not take effect.");
            return;
        }

        int requestedPattern = patternDataList?.FindIndex(
            pattern => pattern != null && pattern.spawnPattern == config.spawnPattern) ?? -1;
        if (requestedPattern >= 0)
        {
            SelectPatternData(requestedPattern);
        }
        else
        {
            Debug.LogWarning(
                $"MapManager: MatchConfig requested layout '{config.spawnPattern}', but Pattern Data List has no matching entry. " +
                "Keeping the selected PatternData entry as the generation authority.",
                this);
        }

        Debug.Log($"<color=lightblue>MapManager:</color> applied {config}");
    }

    #endregion

    #region Start Methods

    // Set Island Spawning Settings - Spawn Patterns 

    void Start()
    {
        RegenerateMap();
    }

    public void GenerateMap()
    {
        var generationStopwatch = System.Diagnostics.Stopwatch.StartNew();
        LastGenerationTimeMs = -1;
        LastGenerationBreakStatus = null;
        failedChunkCount = 0;
        chunksStartedThisRun = 0;
        ClearGenerationProgress();
        LastGenerationFailedChunks = 0;

        ValidateAndMigratePatternData();
        PatternData activePattern = SelectedPatternData;
        if (activePattern == null)
        {
            Debug.LogError(
                "MapManager: generation requires one selected Pattern Data List entry.",
                this);
            return;
        }

        if (activePattern.defaultChunkPrefab == null)
        {
            Debug.LogError(
                $"MapManager: Pattern '{activePattern.displayName}' has no Default Chunk Prefab.",
                this);
            return;
        }

        ResolveGeneratedMapRoot();
        if (generatedMapRoot != null)
        {
            ClearMap();
        }

        // If a lobby handed over a config and nothing consumed it, the match is
        // about to generate with Inspector values and silently ignore the player's
        // choices. Almost always means MatchBootstrapper is missing from the scene.
        if (Application.isPlaying && GameSession.HasPending)
        {
            Debug.LogError("MapManager: a MatchConfig is still pending at generation " +
                           "time - no MatchBootstrapper consumed it. The lobby's " +
                           "settings are being ignored.");
        }

        if (waterObject != null)
        {
            waterObject.SetActive(activePattern.waterOnStart);
            Vector3 waterPosition = waterObject.transform.localPosition;
            waterPosition.y = activePattern.waterHeight;
            waterObject.transform.localPosition = waterPosition;
            EnsureWaterCoversGeneratedLattice();
        }

        islands = new List<Island>();
        nextIslandID = 1;
        gameManager = FindObjectOfType<GameManager>();
        generatedMapRoot = new GameObject(GeneratedMapRootName).transform;
        generatedMapRoot.SetParent(transform, false);

        // Build complete world-positioned landforms before any chunk reads their fields.
        try { terrainField = BuildWorldTerrainField(activePattern); }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            ClearGenerationProgress();
            return;
        }

        // Creates Island Game Object
        // > Start 
        switch (activePattern.spawnPattern)
        {
            case SpawnPattern.Singular: // DEV ONLY
                // Singular Spawn
                int singularIslandID = 1;
                // Minimum corner placed half a chunk negative on both axes, so the one
                // generated chunk is centred on the world origin.
                float Offset = ChunkWorldSize * -0.5f;
               
                for (int i = 0; i < 1; i++)
                {
                    // Singular Spawn

                    // Generate a Single new island
                    IslandData islandData = new IslandData(GridType.Type.Island);

                    // Set the position and size of the island's bounds
                    Vector3 islandPosition = new Vector3(Offset, 0, Offset);
                    islandData.bounds = MakeChunkBounds(islandPosition);

                    islandData.islandType = IslandType.None;
                    islandData.id = singularIslandID;  // Use the reserved ID
                    islandData.name = "Island " + singularIslandID;

                        AddIsland(islandData, 0, 0); // Singular
                }
                break;

            //case SpawnPattern.Linear: // DEV ONLY
            //    // Linear Spawn
            //    for (int i = 0; i < numberOfIslands; i++)
            //    {
            //        // Linear Spawn
            //        // Generate a islands in a row
            //        IslandData islandData = new IslandData(GridType.Type.Island);
            //        islandData.islandType = IslandType.None;
            //        islandData.buildings = new List<Building>();
            //        islandData.items = new Dictionary<ItemData, int>();
            //        islandData.bounds = new Bounds(new Vector3(i * 20, 0, 0), new Vector3(10, 10, 10));
            //        islandData.id = i + 1;
            //        islandData.name = "Island " + (i + 1);
            //        AddIsland(islandData);
            //    }
            //    break;

            //case SpawnPattern.Circular: // DEV ONLY
            //    // Circular Spawn
            //    float worldLimit = 100f; // Define your world limit here
            //    float angleIncrement = 360f / numberOfIslands;
            //    for (int i = 0; i < numberOfIslands; i++)
            //    {
            //        // Calculate the island's position in a circular pattern
            //        float angle = i * angleIncrement * Mathf.Deg2Rad;
            //        Vector3 islandPosition = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * (worldLimit / 2);
            //        // 1. Generate a new island
            //        IslandData islandData = new IslandData(GridType.Type.Island);
            //        // 2. Set the new island's data
            //        islandData.islandType = IslandType.None;
            //        islandData.buildings = new List<Building>(); 
            //        islandData.items = new Dictionary<ItemData, int>();
            //        islandData.bounds = new Bounds(islandPosition, new Vector3(10, 10, 10));
            //        islandData.id = i + 1;
            //        islandData.name = "Island " + (i + 1);
            //        AddIsland(islandData);
            //    }
            //    break;

            case SpawnPattern.Square: // DEV ONLY
                // Square Spawn
                int currentIsland = 0;
                // Centres the lattice's swept MESH extent on the world origin, not the
                // slot positions - the transform is each chunk's minimum corner.
                xOffset = LatticeCenteringOffset;
                zOffset = LatticeCenteringOffset;

                for (int i = 0; i < RunGridSize; i++)
                {
                    for (int j = 0; j < RunGridSize; j++)
                    {
                        // Generate a new island
                        IslandData islandData = new IslandData(GridType.Type.Island);
                        islandData.islandType = IslandType.None;
                        //islandData.buildings = new List<Building>();

                        // Chunk minimum corner, stepped by the LATTICE SLOT pitch.
                        Vector3 islandPosition = new Vector3(i * LatticeSlotSpacing - xOffset, 0, j * LatticeSlotSpacing - zOffset);

                        // Set the remaining data for the island
                        islandData.bounds = MakeChunkBounds(islandPosition);
                        islandData.id = currentIsland + 1;
                        islandData.name = "Island " + (currentIsland + 1);

                        // Add the island to the game world
                        AddIsland(islandData, i, j); // Square

                        currentIsland++;
                    }
                }
                break;
            
            // CURRENT FINAL DEFAULT:
            case SpawnPattern.Normal: // FINAL VERSION
                // Square Spawn + Normal Sized Orbitor Islands

                currentIsland = 0;
                // Centres the lattice's swept MESH extent on the world origin, not the
                // slot positions - the transform is each chunk's minimum corner.
                xOffset = LatticeCenteringOffset;
                zOffset = LatticeCenteringOffset;
                // Add Islands Loop
                for (int i = 0; i < RunGridSize; i++)
                {
                    for (int j = 0; j < RunGridSize; j++)
                    {
                        // Create New Data
                        IslandData islandData = new IslandData(GridType.Type.Island);

                        // Set the position
                        // Chunk minimum corner - the MapGrid transform goes here and its
                        // mesh spans local 0..ChunkWorldSize from it. Stepping by the slot
                        // pitch tiles only because the selection fills every other slot.
                        Vector3 islandPosition = new Vector3(i * LatticeSlotSpacing - xOffset, 0, j * LatticeSlotSpacing - zOffset);

                        // Set Size - Not looking good bro :NAHH:
                        // Yet to be a Fully Written, Use Pre-game "Biome Setting" / Player Setting"


                        // Set island type
                        islandData.islandType = IslandType.None;

                        // Set island type
                        // Setting island type using system
                        // Yet to be a Fully Written, Using:
                        // -> location equation 
                        // -> pre-game settings

                        // Set islands buildings
                        islandData.buildings = new List<Building>();

                        // Set islands items
                        islandData.items = new Dictionary<ItemData, int>();

                        // Set the bounds
                        // Set the remaining data for the island
                        islandData.bounds = MakeChunkBounds(islandPosition);
                        islandData.id = currentIsland + 1;
                        islandData.name = "Island " + (currentIsland + 1);

                        // Generate Island Data

                        // Generate island data logic...
                        // Add the island to the game world
                        AddIsland(islandData, i, j); // Normal

                        currentIsland++;

                    }
                }

                // Map Generation Cycle

                // Creates Map Game Objects
                // > Complete

                // Creates Island Game Object
                // > Complete

                // Add Water Operation
                // Start - Complete

                // Add Plataeu Operation
                // Start - Incomplete - Not Working Atm! 

                // Initialize Start Game Session
                // gameManager.StartGameSession(string Name,int GameSpeed, Bool WinCon);

                // If WinCon exists 
                // false: gameManager.LoseGameSession();
                // true: gameManager.WinGameSession();

                // And then End Game Session
                // gameManager.EndGameSession();


               break;

            default:
                Debug.LogError("Incomplete or Invalid Spawn Pattern Selected!");
                Debug.LogWarning("Select New a Valid or Complete Pattern next time!");
                break;
        }

        ValidateGeneratedChunkSeparation();
        ReportTerrainMeasurements();
        EnsureInfiniteHorizon(activePattern);
        GetComponent<GameBorder>()?.FitToGeneratedMap();

        LastGenerationFailedChunks = failedChunkCount;
        if (failedChunkCount > 0)
        {
            string failureSummary =
                $"MapManager: {failedChunkCount} chunk(s) failed to generate and are missing from the map. " +
                "The rest of the world was generated; see the errors above for the failing slots.";
            LastGenerationBreakStatus = failureSummary;
            Debug.LogError(failureSummary, this);
            OnMapGenerationFailed?.Invoke(this, failureSummary);
        }

        OnMapGenerated?.Invoke();
        ClearGenerationProgress();
        LastGenerationTimeMs = generationStopwatch.ElapsedMilliseconds;
    }

    /// <summary>
    /// Keeps the shared water surface large enough for the complete centred lattice,
    /// including one chunk of ocean margin around its outer edge. This is based on the
    /// mesh's authored local bounds, so it works for the current Unity Plane without
    /// hard-coding its ten-unit primitive size.
    /// </summary>
    private void EnsureWaterCoversGeneratedLattice()
    {
        if (waterObject == null) return;

        MeshFilter waterMesh = waterObject.GetComponent<MeshFilter>();
        if (waterMesh == null || waterMesh.sharedMesh == null) return;

        Vector3 meshSize = waterMesh.sharedMesh.bounds.size;
        if (meshSize.x <= 0f || meshSize.z <= 0f) return;

        float latticeWidth = (Mathf.Max(1, RunGridSize) - 1) * LatticeSlotSpacing + ChunkWorldSize;
        float requiredWidth = latticeWidth + ChunkWorldSize * 2f;
        Vector3 waterScale = waterObject.transform.localScale;
        waterScale.x = Mathf.Max(waterScale.x, requiredWidth / meshSize.x);
        waterScale.z = Mathf.Max(waterScale.z, requiredWidth / meshSize.z);
        waterObject.transform.localScale = waterScale;
    }

    private const string GeneratedHorizonRootName = "Generated Infinite Horizon";
    private const float InfiniteHorizonOuterRadius = 3600f;
    private readonly List<UnityEngine.Object> generatedHorizonResources = new List<UnityEngine.Object>();

    private void ReleaseHorizonResources()
    {
        for (int i = 0; i < generatedHorizonResources.Count; i++)
        {
            UnityEngine.Object obj = generatedHorizonResources[i];
            if (obj == null) continue;
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }
        generatedHorizonResources.Clear();
    }

    private void EnsureInfiniteHorizon(PatternData activePattern)
    {
        if (generatedMapRoot == null) return;

        Transform existing = generatedMapRoot.Find(GeneratedHorizonRootName);
        if (existing != null)
        {
            if (Application.isPlaying) Destroy(existing.gameObject);
            else DestroyImmediate(existing.gameObject);
        }
        ReleaseHorizonResources();

        Renderer oceanDatumRenderer = waterObject != null ? waterObject.GetComponent<Renderer>() : null;
        MapGrid[] chunks = generatedMapRoot.GetComponentsInChildren<MapGrid>(true);
        if (chunks.Length == 0)
        {
            OceanSurfaceBuilder.Disable(oceanDatumRenderer);
            return;
        }

        Bounds mapBounds = default;
        bool hasMapBounds = false;
        Material terrainSplatMaterial = null;
        ClimateProfile climate = null;
        float abyssY = float.NaN;

        for (int i = 0; i < chunks.Length; i++)
        {
            MapGrid g = chunks[i];
            if (g == null) continue;
            MeshRenderer mr = g.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                if (!hasMapBounds) { mapBounds = mr.bounds; hasMapBounds = true; }
                else mapBounds.Encapsulate(mr.bounds);

                if (terrainSplatMaterial == null && mr.sharedMaterial != null
                    && mr.sharedMaterial.shader != null
                    && mr.sharedMaterial.shader.name == "Moonlight/Terrain Splat (Normal Mapped)")
                {
                    terrainSplatMaterial = mr.sharedMaterial;
                }
            }

            if (terrainSplatMaterial == null && g.terrainMaterial != null)
                terrainSplatMaterial = g.terrainMaterial;
            if (climate == null && g.climateProfile != null)
                climate = g.climateProfile;
            if (float.IsNaN(abyssY) && g.TerrainSource != null && g.TerrainSource.Settings != null)
                abyssY = g.TerrainSource.Settings.abyssHeight;
        }

        if (!hasMapBounds)
        {
            OceanSurfaceBuilder.Disable(oceanDatumRenderer);
            return;
        }
        if (float.IsNaN(abyssY)) abyssY = mapBounds.min.y;

        GameObject horizonRoot = new GameObject(GeneratedHorizonRootName);
        horizonRoot.transform.SetParent(generatedMapRoot, false);

        // 0. The visible ocean over the water cube's footprint: tessellated, wave-displaced and
        //    aware of every coast. Returns null (cube keeps rendering) if disabled/unavailable.
        Material shoreOceanMaterial = null;
        if (waterObject != null && waterObject.activeInHierarchy && oceanDatumRenderer != null)
        {
            shoreOceanMaterial = OceanSurfaceBuilder.Build(
                horizonRoot.transform, oceanDatumRenderer, chunks, oceanSurface, generatedHorizonResources);
        }
        else
        {
            OceanSurfaceBuilder.Disable(oceanDatumRenderer);
        }

        // 1. Extended ocean water frame surrounding Ocean Cube cleanly out to +-3600m (no center overlap).
        //    With the shore ocean it shares that material; all wave motion fades to zero at the
        //    cube's edge, so the flat frame meets the displaced surface without a crack.
        if (waterObject != null && waterObject.activeInHierarchy)
        {
            Renderer waterRenderer = oceanDatumRenderer;
            if (waterRenderer != null && (shoreOceanMaterial != null || waterRenderer.sharedMaterial != null))
            {
                Bounds wb = waterRenderer.bounds;
                float waterSurfaceY = wb.max.y;
                float outer = InfiniteHorizonOuterRadius;
                Mesh waterFrame = BuildHollowFrameMesh(
                    "Infinite Ocean Horizon Mesh",
                    wb.min.x, wb.max.x, wb.min.z, wb.max.z,
                    outer,
                    waterSurfaceY,
                    new Vector2(wb.center.x, wb.center.z),
                    new Vector2(Mathf.Max(1f, wb.size.x), Mathf.Max(1f, wb.size.z)));
                generatedHorizonResources.Add(waterFrame);

                GameObject oceanSkirt = new GameObject("Infinite Ocean Horizon", typeof(MeshFilter), typeof(MeshRenderer));
                oceanSkirt.layer = waterObject.layer;
                oceanSkirt.transform.SetParent(horizonRoot.transform, false);
                oceanSkirt.GetComponent<MeshFilter>().sharedMesh = waterFrame;
                MeshRenderer skirtRenderer = oceanSkirt.GetComponent<MeshRenderer>();
                skirtRenderer.sharedMaterial = shoreOceanMaterial != null ? shoreOceanMaterial : waterRenderer.sharedMaterial;
                skirtRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                skirtRenderer.receiveShadows = waterRenderer.receiveShadows;
            }
        }

        // 2. Extended abyssal seafloor frame matching Terrain Splat seabed seamlessly out to +-3600m.
        if (terrainSplatMaterial != null)
        {
            Mesh abyssFrame = BuildHollowFrameMesh(
                "Infinite Abyss Floor Mesh",
                mapBounds.min.x, mapBounds.max.x, mapBounds.min.z, mapBounds.max.z,
                InfiniteHorizonOuterRadius,
                abyssY,
                Vector2.zero,
                new Vector2(ChunkWorldSize, ChunkWorldSize));
            generatedHorizonResources.Add(abyssFrame);

            GameObject abyssSkirt = new GameObject("Infinite Abyss Floor", typeof(MeshFilter), typeof(MeshRenderer));
            abyssSkirt.layer = chunks[0].gameObject.layer;
            abyssSkirt.transform.SetParent(horizonRoot.transform, false);
            abyssSkirt.GetComponent<MeshFilter>().sharedMesh = abyssFrame;
            MeshRenderer abyssRenderer = abyssSkirt.GetComponent<MeshRenderer>();
            abyssRenderer.sharedMaterial = terrainSplatMaterial;
            abyssRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            abyssRenderer.receiveShadows = true;

            Color sand1 = climate != null ? climate.sandColor1 : new Color(0.75f, 0.75f, 0.7f);
            Color sand2 = climate != null ? climate.sandColor2 : new Color(0.7f, 0.7f, 0.65f);
            Color rock1 = climate != null ? climate.rockColor1 : new Color(0.45f, 0.45f, 0.45f);
            Color rock2 = climate != null ? climate.rockColor2 : new Color(0.35f, 0.35f, 0.35f);
            Color abyssSilt = Color.Lerp(((sand1 + sand2) * 0.5f) * 0.72f, ((rock1 + rock2) * 0.5f) * 0.65f, 0.45f);
            abyssSilt.a = 0f;

            Texture2D tintTex = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "Infinite Abyss Surface Tint",
                wrapMode = TextureWrapMode.Clamp
            };
            tintTex.SetPixel(0, 0, abyssSilt);
            tintTex.Apply(false, true);
            generatedHorizonResources.Add(tintTex);

            MaterialPropertyBlock block = new MaterialPropertyBlock();
            block.SetTexture("_Control", Texture2D.blackTexture);
            block.SetTexture("_SurfaceTint", tintTex);
            block.SetFloat("_UseSurfaceTint", 1f);
            abyssRenderer.SetPropertyBlock(block);
        }
    }

    private static Mesh BuildHollowFrameMesh(
        string meshName,
        float innerMinX,
        float innerMaxX,
        float innerMinZ,
        float innerMaxZ,
        float outerRadius,
        float y,
        Vector2 uvCenter,
        Vector2 uvScale)
    {
        float oMinX = Mathf.Min(-outerRadius, innerMinX - 10f);
        float oMaxX = Mathf.Max(outerRadius, innerMaxX + 10f);
        float oMinZ = Mathf.Min(-outerRadius, innerMinZ - 10f);
        float oMaxZ = Mathf.Max(outerRadius, innerMaxZ + 10f);

        Vector3[] verts = new Vector3[16];
        Vector3[] normals = new Vector3[16];
        Vector2[] uvs = new Vector2[16];
        int[] tris = new int[24];

        void AddQuad(int q, float x0, float x1, float z0, float z1)
        {
            int v = q * 4;
            verts[v + 0] = new Vector3(x0, y, z0);
            verts[v + 1] = new Vector3(x1, y, z0);
            verts[v + 2] = new Vector3(x1, y, z1);
            verts[v + 3] = new Vector3(x0, y, z1);

            for (int k = 0; k < 4; k++)
            {
                normals[v + k] = Vector3.up;
                uvs[v + k] = new Vector2(
                    (verts[v + k].x - uvCenter.x) / uvScale.x + 0.5f,
                    (verts[v + k].z - uvCenter.y) / uvScale.y + 0.5f);
            }

            int t = q * 6;
            tris[t + 0] = v + 0;
            tris[t + 1] = v + 2;
            tris[t + 2] = v + 1;
            tris[t + 3] = v + 0;
            tris[t + 4] = v + 3;
            tris[t + 5] = v + 2;
        }

        // North, South, West, East strips leaving exact inner hole uncovered
        AddQuad(0, oMinX, oMaxX, innerMaxZ, oMaxZ);
        AddQuad(1, oMinX, oMaxX, oMinZ, innerMinZ);
        AddQuad(2, oMinX, innerMinX, innerMinZ, innerMaxZ);
        AddQuad(3, innerMaxX, oMaxX, innerMinZ, innerMaxZ);

        Mesh mesh = new Mesh { name = meshName };
        mesh.vertices = verts;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>
    /// Guards the spatial contract: no two GENERATED chunks may overlap.
    ///
    /// Chunks tile only because the current selection fills every other lattice slot,
    /// so occupied-slot stride (2) * latticeSlotSpacing (30) happens to equal
    /// chunkWorldSize (60). Nothing structurally enforces that - a spawn pattern or
    /// selection list that puts two chunks in adjacent slots would silently overlap
    /// them by half a chunk, which is exactly the failure that produced one chunk's
    /// abyssal rim being drawn through its neighbour's centre. This reports it loudly
    /// instead of leaving it to be discovered from a screenshot.
    ///
    /// Footprints come from transform + ChunkWorldSize rather than renderer bounds so
    /// the check is about the spatial contract only, independent of terrain height.
    /// </summary>
    private void ValidateGeneratedChunkSeparation()
    {
        if (generatedMapRoot == null) return;

        MapGrid[] chunks = generatedMapRoot.GetComponentsInChildren<MapGrid>(true);
        float width = ChunkWorldSize;
        int overlaps = 0;
        float observedPitch = float.MaxValue;

        for (int a = 0; a < chunks.Length; a++)
        {
            for (int b = a + 1; b < chunks.Length; b++)
            {
                Vector3 pa = chunks[a].transform.position;
                Vector3 pb = chunks[b].transform.position;

                // Axis-aligned footprints, so an overlap on BOTH axes is a real
                // intersection. Touching exactly (delta == width) is correct tiling.
                float overlapX = width - Mathf.Abs(pa.x - pb.x);
                float overlapZ = width - Mathf.Abs(pa.z - pb.z);
                if (overlapX <= 0f || overlapZ <= 0f) continue;

                overlaps++;

                // The pitch the selection actually produced, so the summary below can name the
                // slotSpacing that would make chunks tile instead of only saying they do not.
                float pitch = Mathf.Max(Mathf.Abs(pa.x - pb.x), Mathf.Abs(pa.z - pb.z));
                if (pitch > 0.01f && pitch < observedPitch) observedPitch = pitch;
                Debug.LogError(
                    $"MapManager: generated chunks '{chunks[a].name}' and '{chunks[b].name}' OVERLAP by " +
                    $"{overlapX:F1} x {overlapZ:F1} world units. chunkWorldSize={width}, " +
                    $"latticeSlotSpacing={LatticeSlotSpacing}. Two selected slots are closer than one " +
                    $"chunk width apart - the selection must keep an occupied-slot stride of at least " +
                    $"{Mathf.CeilToInt(width / Mathf.Max(0.0001f, LatticeSlotSpacing))}.", chunks[a]);
            }
        }

        if (overlaps > 0)
        {
            // ChunkWorldSize follows the island prefab's MapGrid.size automatically, but
            // slotSpacing is authored on the PatternData and does not. Changing island size
            // therefore breaks the tiling contract silently from the pattern's point of view,
            // which is exactly how a 60 -> 90 resize left every chunk overlapping its
            // neighbour by 30 units. Naming the required value turns this into a one-field fix.
            string remedy = string.Empty;
            if (observedPitch < float.MaxValue && LatticeSlotSpacing > 0.0001f)
            {
                int stride = Mathf.Max(1, Mathf.RoundToInt(observedPitch / LatticeSlotSpacing));
                remedy = $" Occupied slots are {observedPitch:F1} apart (stride {stride}), so set the " +
                    $"pattern's slotSpacing to {width / stride:F1} to make chunks of width {width} tile exactly.";
            }

            Debug.LogError(
                $"MapManager: {overlaps} overlapping chunk pair(s) - the map's spatial contract is violated." + remedy,
                this);
        }
    }

    public void RegenerateMap()
    {
        ClearMap();
        GenerateMap();
    }

    public void ClearMap()
    {
        terrainField = null;
        ResolveGeneratedMapRoot();
        if (generatedMapRoot != null)
        {
            GameObject rootObject = generatedMapRoot.gameObject;
            generatedMapRoot = null;

            foreach (MapGrid mapGrid in rootObject.GetComponentsInChildren<MapGrid>(true))
            {
                mapGrid.ReleaseGeneratedVisualResources();
            }

            ReleaseHorizonResources();
            if (Application.isPlaying) Destroy(rootObject);
            else DestroyImmediate(rootObject);
        }
        OceanSurfaceBuilder.Disable(waterObject != null ? waterObject.GetComponent<Renderer>() : null);

        islands ??= new List<Island>();
        islands.Clear();
        oceanGizmoPositions.Clear();
        nextIslandID = 1;

        if (!Application.isPlaying && waterObject != null)
        {
            waterObject.SetActive(false);
        }
    }

    private void ResolveGeneratedMapRoot()
    {
        if (generatedMapRoot == null && !Application.isPlaying)
        {
            generatedMapRoot = transform.Find(GeneratedMapRootName);
        }
    }

    /// <summary>
    /// Rehydrates the non-serialized procedural state that Unity discards during an
    /// editor domain reload. The saved chunk objects are the durable generation
    /// recipe; each MapGrid deterministically rebuilds its cells, mesh, texture, and
    /// plateau geometry from its serialized type, settings, transform, and island id.
    /// </summary>
    public void RestoreGeneratedStateAfterDomainReload()
    {
        if (Application.isPlaying) return;

        ResolveGeneratedMapRoot();
        if (generatedMapRoot == null) return;

        MapGrid[] generatedChunks = generatedMapRoot.GetComponentsInChildren<MapGrid>(true);
        if (generatedChunks.Length == 0) return;

        islands = generatedMapRoot.GetComponentsInChildren<Island>(true).ToList();
        nextIslandID = islands.Count == 0
            ? 1
            : islands.Max(island => island != null ? island.id : 0) + 1;

        foreach (MapGrid generatedChunk in generatedChunks)
        {
            generatedChunk.RestoreGeneratedStateAfterDomainReload();
        }

        EnsureInfiniteHorizon(SelectedPatternData);
        GetComponent<GameBorder>()?.FitToGeneratedMap();
    }
    
    #endregion

    #region Island +/- Methods 
    public List<Vector3> oceanGizmoPositions = new List<Vector3>();

    // IMPORTANT: USES SPAWN PATTERNS 
    /// <summary>
    /// A whole-map regeneration is ONE synchronous call on the main thread: every chunk is
    /// built inline, so the editor pumps no Windows messages from the first chunk to the last.
    /// Windows treats an application that has not serviced its message queue for long enough as
    /// hung and terminates it - observed here as Application Hang / AppHangB1 with no exception,
    /// no managed stack and no crash dump, which reads like a generation bug and is not one.
    /// A 16-chunk map crossed that threshold once islands grew to 90 cells.
    ///
    /// Showing the progress bar per chunk services the message queue, so the process stays
    /// alive for the whole run, and it names the chunk being built - which is also the only
    /// in-editor signal of where a long generation actually is.
    /// </summary>
    private void ReportGenerationProgress(string chunkName)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            float total = Mathf.Max(1, RunGridSize * RunGridSize);
            UnityEditor.EditorUtility.DisplayProgressBar(
                "Generating map",
                $"Chunk {chunksStartedThisRun + 1}: {chunkName}",
                Mathf.Clamp01(chunksStartedThisRun / total));
        }
#endif
        chunksStartedThisRun++;
    }

    private void ClearGenerationProgress()
    {
#if UNITY_EDITOR
        if (Application.isPlaying) return;
        UnityEditor.EditorUtility.ClearProgressBar();
#endif
    }

    [ContextMenu("Report terrain seams and histograms")]
    public void ReportTerrainMeasurements()
    {
        var grids = GetComponentsInChildren<MapGrid>();
        for (int a = 0; a < grids.Length; a++)
        {
            var left = grids[a];
            if (left.TerrainSource == null) continue;
            var ca = left.TerrainSource.GetOrCreateSampleCache(left.generationSettings.visualSamplesPerCell);
            var histogram = new Dictionary<Cell.TerrainType, int>();
            double totalHeight = 0;
            int density = ca.VisualSamplesPerCell;
            for (int z = 0; z < left.size; z++) for (int x = 0; x < left.size; x++)
            {
                int i = ca.GetIndex(x * density + density / 2, z * density + density / 2);
                var type = ca.TerrainTypes[i];
                histogram.TryGetValue(type, out int amount); histogram[type] = amount + 1;
                totalHeight += ca.Heights[i];
            }
            Debug.Log($"[Terrain cells {left.name}] summed heights={totalHeight:F6}; "
                + string.Join(", ", histogram.OrderBy(p => p.Key).Select(p => $"{p.Key}={p.Value}")));
            for (int b = a + 1; b < grids.Length; b++)
            {
                var right = grids[b];
                if (right.TerrainSource == null || left.size != right.size) continue;
                Vector3 delta = right.transform.position - left.transform.position;
                bool alongX = Mathf.Abs(Mathf.Abs(delta.x) - left.size) < 0.001f && Mathf.Abs(delta.z) < 0.001f;
                bool alongZ = Mathf.Abs(Mathf.Abs(delta.z) - left.size) < 0.001f && Mathf.Abs(delta.x) < 0.001f;
                if (!alongX && !alongZ) continue;
                var cb = right.TerrainSource.GetOrCreateSampleCache(right.generationSettings.visualSamplesPerCell);
                int r = ca.Resolution;
                double sum = 0; float maximum = 0;
                for (int t = 0; t < r; t++)
                {
                    int ia = alongX ? ca.GetIndex(delta.x > 0 ? r - 1 : 0, t) : ca.GetIndex(t, delta.z > 0 ? r - 1 : 0);
                    int ib = alongX ? cb.GetIndex(delta.x > 0 ? 0 : r - 1, t) : cb.GetIndex(t, delta.z > 0 ? 0 : r - 1);
                    float difference = Mathf.Abs(ca.Heights[ia] - cb.Heights[ib]);
                    sum += difference; maximum = Mathf.Max(maximum, difference);
                }
                string message = $"[Terrain seam {left.name} -> {right.name}] mean={sum / r:F6}, max={maximum:F6}, samples={r}";
                if (maximum > 0.001f) Debug.LogError(message); else Debug.Log(message);
            }
        }
    }

    private WorldTerrainField terrainField;
    public WorldTerrainField GetTerrainField()
    {
        if (terrainField == null)
        {
            try { terrainField = BuildWorldTerrainField(SelectedPatternData); }
            finally { ClearGenerationProgress(); }
        }
        return terrainField;
    }


    private struct SlotPlacementInfo
    {
        public int Slot;
        public GridType.Type Kind;
        public GameObject Prefab;
        public IslandGenerationOverrides Overrides;
        public Rect Bounds;
    }

    private WorldTerrainField BuildWorldTerrainField(PatternData pattern)
    {
        var result = new WorldTerrainField();
        int count = pattern.spawnPattern == SpawnPattern.Singular ? 1 : RunGridSize;
        float offset = count == 1 ? ChunkWorldSize * 0.5f : LatticeCenteringOffset;
        float sea = ResolveOceanSurfaceHeight(pattern);
        int dominantSlot = -1;
        float closestToCentre = float.PositiveInfinity;
        var activeSlots = new List<SlotPlacementInfo>();
        for (int row = 0; row < count; row++) for (int column = 0; column < count; column++)
        {
            int slot = count == 1 ? 1 : row * count + column + 1;
            if (!pattern.TryResolveSlot(slot, row, column, out var kind, out var prefab, out var overrides)) continue;
            var bounds = new Rect(row * LatticeSlotSpacing - offset, column * LatticeSlotSpacing - offset, ChunkWorldSize, ChunkWorldSize);
            activeSlots.Add(new SlotPlacementInfo
            {
                Slot = slot,
                Kind = kind,
                Prefab = prefab,
                Overrides = overrides,
                Bounds = bounds
            });
            if (kind != GridType.Type.Island) continue;
            Vector2 centre = bounds.center;
            if (centre.sqrMagnitude >= closestToCentre) continue;
            closestToCentre = centre.sqrMagnitude; dominantSlot = slot;
        }
        float worldMin = -offset;
        float worldMax = (count - 1) * LatticeSlotSpacing - offset + ChunkWorldSize;
        for (int s = 0; s < activeSlots.Count; s++)
        {
            SlotPlacementInfo entry = activeSlots[s];
            int slot = entry.Slot;
            GridType.Type kind = entry.Kind;
            var template = entry.Prefab != null ? entry.Prefab.GetComponent<MapGrid>() : null;
            if (template == null) throw new InvalidOperationException($"Slot {slot} has no MapGrid prefab.");
            var authored = template.worldSettings != null ? template.worldSettings.Template : template.generationSettings;
            var settings = template.CreateLandformSettings(entry.Overrides, pattern.plateauSettings, sea);
            int seed = unchecked(settings.seed * 397 ^ slot);
            Vector2 origin = entry.Bounds.min;
            int shapeSize = template.size;
            int padding = 0;
            if (kind == GridType.Type.Island)
            {
                var random = new System.Random(seed);
                if (count > 1)
                {
                    Rect safeBounds = ComputeSafeIslandDomain(entry.Bounds, activeSlots, worldMin, worldMax, worldMin, worldMax);
                    // Landform extents and positions belong to the world, independently of tile storage.
                    float extent = slot != dominantSlot ? Mathf.Lerp(settings.secondaryLandformScale.x, settings.secondaryLandformScale.y, (float)random.NextDouble()) : settings.dominantLandformScale;
                    shapeSize = Mathf.RoundToInt(template.size * extent);
                    int maxAllowedSize = Mathf.FloorToInt(Mathf.Min(safeBounds.width, safeBounds.height));
                    if (maxAllowedSize > 0) shapeSize = Mathf.Min(shapeSize, maxAllowedSize);
                    origin += Vector2.one * ((template.size - shapeSize) * 0.5f);
                    origin += new Vector2(Mathf.Round((float)(random.NextDouble() - 0.5) * template.size * Mathf.Clamp(settings.landformPositionJitter, 0f, 0.5f) * 2f),
                        Mathf.Round((float)(random.NextDouble() - 0.5) * template.size * Mathf.Clamp(settings.landformPositionJitter, 0f, 0.5f) * 2f));
                    // Preserve one common sample lattice even for odd landform sizes.
                    origin.x = Mathf.Round(origin.x + offset) - offset;
                    origin.y = Mathf.Round(origin.y + offset) - offset;
                    float maxOriginX = Mathf.Max(safeBounds.xMin, safeBounds.xMax - shapeSize);
                    float maxOriginZ = Mathf.Max(safeBounds.yMin, safeBounds.yMax - shapeSize);
                    origin.x = Mathf.Clamp(origin.x, safeBounds.xMin, maxOriginX);
                    origin.y = Mathf.Clamp(origin.y, safeBounds.yMin, maxOriginZ);
                }
                var coast = settings.coastalProfile;
                padding = Mathf.CeilToInt(coast.surfWidth + coast.shelfWidth + coast.continentalSlopeWidth
                    + coast.deepHandoffWidth + coast.curvatureStencilRadius + 2f);
            }
            ReportGenerationProgress($"World landform {slot}");
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var source = new IslandTerrainProvider(settings, kind, shapeSize, seed, settings.seed, origin, padding);
            result.Add(slot, source, watch.ElapsedMilliseconds);
            Debug.Log($"[World terrain slot {slot}] landform {shapeSize}, margin {padding}, "
                + $"origin {origin}, generation {watch.ElapsedMilliseconds} ms; "
                + $"beach variation authored/resolved {authored?.coastalProfile.widthVariation}/{settings.coastalProfile.widthVariation}, "
                + $"ridge threshold authored/resolved {authored?.coastalProfile.headlandRidgeThreshold}/{settings.coastalProfile.headlandRidgeThreshold}");
        }
        return result;
    }

    private static Rect ComputeSafeIslandDomain(
        Rect chunkBounds,
        List<SlotPlacementInfo> activeSlots,
        float worldMinX,
        float worldMaxX,
        float worldMinZ,
        float worldMaxZ)
    {
        var blockers = new List<Rect>();
        for (int i = 0; i < activeSlots.Count; i++)
        {
            if (activeSlots[i].Kind != GridType.Type.Island) blockers.Add(activeSlots[i].Bounds);
        }

        Vector2 centre = chunkBounds.center;
        blockers.Sort((a, b) => (a.center - centre).sqrMagnitude.CompareTo((b.center - centre).sqrMagnitude));

        float sMinX = worldMinX;
        float sMaxX = worldMaxX;
        float sMinZ = worldMinZ;
        float sMaxZ = worldMaxZ;

        for (int i = 0; i < blockers.Count; i++)
        {
            Rect b = blockers[i];
            if (b.xMax <= sMinX + 0.001f || b.xMin >= sMaxX - 0.001f
                || b.yMax <= sMinZ + 0.001f || b.yMin >= sMaxZ - 0.001f)
            {
                continue;
            }

            float bestMinDim = -1f;
            float bestArea = -1f;
            float nextMinX = sMinX, nextMaxX = sMaxX, nextMinZ = sMinZ, nextMaxZ = sMaxZ;

            void EvaluateTrim(float cMinX, float cMaxX, float cMinZ, float cMaxZ)
            {
                float w = cMaxX - cMinX;
                float h = cMaxZ - cMinZ;
                if (w < chunkBounds.width - 0.001f || h < chunkBounds.height - 0.001f) return;
                float minDim = Mathf.Min(w, h);
                float area = w * h;
                if (minDim > bestMinDim + 0.001f
                    || (Mathf.Abs(minDim - bestMinDim) <= 0.001f && area > bestArea))
                {
                    bestMinDim = minDim;
                    bestArea = area;
                    nextMinX = cMinX;
                    nextMaxX = cMaxX;
                    nextMinZ = cMinZ;
                    nextMaxZ = cMaxZ;
                }
            }

            if (b.xMax <= chunkBounds.xMin + 0.001f) EvaluateTrim(Mathf.Max(sMinX, b.xMax), sMaxX, sMinZ, sMaxZ);
            if (b.xMin >= chunkBounds.xMax - 0.001f) EvaluateTrim(sMinX, Mathf.Min(sMaxX, b.xMin), sMinZ, sMaxZ);
            if (b.yMax <= chunkBounds.yMin + 0.001f) EvaluateTrim(sMinX, sMaxX, Mathf.Max(sMinZ, b.yMax), sMaxZ);
            if (b.yMin >= chunkBounds.yMax - 0.001f) EvaluateTrim(sMinX, sMaxX, sMinZ, Mathf.Min(sMaxZ, b.yMin));

            if (bestMinDim > 0f)
            {
                sMinX = nextMinX;
                sMaxX = nextMaxX;
                sMinZ = nextMinZ;
                sMaxZ = nextMaxZ;
            }
        }

        // Guard against floating-point edge overlap on inclusive rect lookups.
        sMinX = Mathf.Min(sMinX, chunkBounds.xMin);
        sMaxX = Mathf.Max(sMaxX, chunkBounds.xMax);
        sMinZ = Mathf.Min(sMinZ, chunkBounds.yMin);
        sMaxZ = Mathf.Max(sMaxZ, chunkBounds.yMax);

        return Rect.MinMaxRect(sMinX, sMinZ, sMaxX, sMaxZ);
    }

    public void AddIsland(IslandData data, int row = 0, int column = 0)
    {
        PatternData activePattern = SelectedPatternData;
        if (activePattern == null)
        {
            Debug.LogError("MapManager: cannot resolve a slot without selected PatternData.", this);
            return;
        }

        if (!activePattern.TryResolveSlot(
                data.id,
                row,
                column,
                out GridType.Type resolvedTerrain,
                out GameObject prefabToUse,
                out IslandGenerationOverrides slotOverrides))
        {
            return;
        }

        data.gridType = resolvedTerrain;
        bool shouldAddOcean = resolvedTerrain == GridType.Type.Ocean;
        if (prefabToUse == null)
        {
            Debug.LogError(
                $"MapManager: Pattern '{activePattern.displayName}' resolved slot {data.id} without a chunk prefab.",
                this);
            return;
        }

            GameObject islandGO = Instantiate(prefabToUse);
            islandGO.transform.SetParent(generatedMapRoot, true);
            // The MapGrid transform is the chunk's MINIMUM CORNER, not its centre: the
            // terrain mesh spans local 0..ChunkWorldSize from the transform. MakeChunkBounds
            // put the bounds centre half a chunk past that corner, so undo it here.
            float halfChunk = ChunkWorldSize * 0.5f;
            islandGO.transform.position = new Vector3(
                data.bounds.center.x - halfChunk,
                data.bounds.center.y,
                data.bounds.center.z - halfChunk);

            // Ocean macro-grids are built from the same prefab and pipeline as islands,
            // but calling them "Island N" makes the hierarchy unreadable when half the
            // map is open water. Only the GameObject is renamed - IslandData.name and
            // the island id are left alone so nothing that looks them up breaks.
            islandGO.name = data.gridType == GridType.Type.Island
                ? data.name
                : $"{data.gridType} {data.id}";

            Island island = islandGO.GetComponent<Island>();
            if (island == null)
            {
                Debug.LogError($"MapManager: '{prefabToUse.name}' has no Island component - cannot add chunk '{data.name}'.");
                Destroy(islandGO);
                return;
            }

            island.Initialize(data.islandType);
            island.islandConfig = activePattern.configuration;
            island.buildings = data.buildings;
            island.IslandItems = data.items;
            island.bounds = data.bounds;
            island.id = data.id > 0 ? data.id : nextIslandID;
            nextIslandID = Mathf.Max(nextIslandID, island.id + 1);
            island.islandObject = islandGO;

            islands.Add(island);

            GridSystem gridSystem = islandGO.GetComponent<GridSystem>();
            MapGrid mapGrid = islandGO.GetComponent<MapGrid>();
            InfluenceManager influenceManager = islandGO.AddComponent<InfluenceManager>();

            if (mapGrid == null)
            {
                Debug.LogError($"MapManager: '{prefabToUse.name}' has no MapGrid component.", islandGO);
                Destroy(islandGO);
                islands.Remove(island);
                return;
            }

            mapGrid.currentGridType = resolvedTerrain;

            // PatternData owns the visible ocean surface and plateau profile. Hand both to every
            // chunk so island borders and plateau surrounds derive the same abyss datum and remain
            // seamless after a domain reload.
            //
            // Handed over rather than assigned onto generationSettings directly: a chunk with a
            // world settings asset REPLACES its settings object when it resolves the shared
            // contract at generation time, so anything written here beforehand would be discarded.
            if (slotOverrides != null) mapGrid.generationOverrides = slotOverrides;

            mapGrid.SetWorldContext(
                activePattern.plateauSettings.Clone(),
                ResolveOceanSurfaceHeight(activePattern));

            // One chunk must never take the whole map down with it. Generation used to
            // run unguarded, so a single throwing chunk aborted the spawn loop midway:
            // every later slot was silently never created, ValidateGeneratedChunkSeparation
            // never ran, and OnMapGenerated never fired - the world came up partially
            // built with nothing saying so. Failing one chunk loudly and continuing keeps
            // the rest of the map, and the error names the slot that has to be looked at.
            ReportGenerationProgress(islandGO.name);
            try
            {
                mapGrid.SetTerrainField(terrainField);
                mapGrid.InitializeTerrain();
            }
            catch (System.Exception exception)
            {
                Debug.LogError(
                    $"MapManager: chunk '{islandGO.name}' (slot {data.id}, {resolvedTerrain}) failed to generate " +
                    $"and was removed from the map. {exception.GetType().Name}: {exception.Message}",
                    islandGO);
                Debug.LogException(exception, islandGO);
                failedChunkCount++;
                islands.Remove(island);
                Destroy(islandGO);
                return;
            }


            // If this ID is marked for ocean and we're not inverting, or it's an island and we are inverting
            if (shouldAddOcean)
            {
                // Add the position for gizmo drawing
                oceanGizmoPositions.Add(island.bounds.center + Vector3.up * 5); // Raise the Gizmo above the terrain for visibility
                // Debug.Log($"Ocean placed at {island.bounds.center} with ID {island.id}");
            }
    }

    /// <summary>
    /// The height the ocean actually RENDERS at, which is what the coastal profile must be built
    /// against. The pattern's waterHeight is a transform Y, and the scene's water is a scaled cube
    /// whose surface is half its height above that - "Ocean Cube - Water Cube" at y = -4 with
    /// scale.y = 1 puts its surface at -3.5, half a unit from what the pattern claims. Terrain
    /// generated against the wrong figure puts the beach at the wrong height relative to the sea,
    /// which no amount of beach tuning can correct.
    ///
    /// Reading the renderer's world bounds handles a plane, a cube, or anything else, and falls
    /// back to the authored value when the object has no renderer.
    /// </summary>
    private float ResolveOceanSurfaceHeight(PatternData activePattern)
    {
        if (waterObject == null) return activePattern.waterHeight;

        Renderer waterRenderer = waterObject.GetComponent<Renderer>();
        if (waterRenderer == null) return activePattern.waterHeight;

        // The datum has to come from water the player can actually SEE. A reference left on a
        // hidden object still reports perfectly good bounds, so a wrong sea level propagates into
        // every coastal band, beach height and land freeboard without a single warning - the
        // islands just come up submerged and every beach control looks broken instead.
        //
        // This has already cost a full debugging pass twice: once when the reference pointed at an
        // inactive 'Ocean Plane' 2.5 units below the visible ocean cube, and again when an editor
        // crash discarded the corrected reference and generation silently reverted to it. Scene
        // references do not survive a crash; this check does.
        if (!waterObject.activeInHierarchy || !waterRenderer.enabled)
        {
            Debug.LogError(
                $"MapManager: Water Object is '{waterObject.name}', which is not being rendered " +
                $"(activeInHierarchy {waterObject.activeInHierarchy}, renderer enabled {waterRenderer.enabled}). " +
                $"Its surface reads {waterRenderer.bounds.max.y:F2}, but nothing draws it, so terrain would be " +
                $"built against a waterline the player never sees. Falling back to the pattern's waterHeight " +
                $"{activePattern.waterHeight:F2}. Assign the ocean object that is actually visible in the scene.",
                this);
            return activePattern.waterHeight;
        }

        float surface = waterRenderer.bounds.max.y;
        if (!Mathf.Approximately(surface, activePattern.waterHeight))
        {
            Debug.Log(
                $"MapManager: ocean surface resolved to {surface:F2} from '{waterObject.name}' " +
                $"(transform Y {waterObject.transform.position.y:F2}); the pattern's waterHeight is " +
                $"{activePattern.waterHeight:F2}. Terrain is built against {surface:F2}.", this);
        }

        return surface;
    }

    // At the class level
    [SerializeField] private bool showGizmos = true; // Default to true to show gizmos initially

    // Within your existing OnDrawGizmos method
    void OnDrawGizmos()
    {
        if (showGizmos)
        {
            Gizmos.color = Color.red; // Set the color of the gizmos to red
            foreach (var pos in oceanGizmoPositions)
            {
                Gizmos.DrawSphere(pos, 2.5f); // Draw a sphere at each position with a radius of 2.5 units
            }
        }
    }


    private bool shouldBePlateau(IslandData islandData)
    {
        // Define logic here to determine if this should be a plateau
        // if it should be then return the type we desire
        return islandData.gridType == GridType.Type.Plateau;
    }

    public void RemoveIsland(Island island)
    {
        islands.Remove(island);
    }

    #endregion

    #region Remove Selection Methods
    [SerializeField] private GameObject flatLandPrefab; // Reference to the flat land prefab
    [SerializeField] private bool showOceanLocations = false; // Toggle to visualize ocean placements

    // Version 2
    public void RemoveSelectedIslandsAndOceans(bool invertSelection)
    {
        List<int> mergedSelection = new List<int>(ActiveIslandSelection);
        mergedSelection.AddRange(ActiveOceanSelection.Except(ActiveIslandSelection)); // Combine and avoid duplicates

        List<Island> toRemove = new List<Island>();

        // Add the islands and oceans to remove based on the merged selection
        foreach (Island island in islands)
        {
            bool isSelected = mergedSelection.Contains(island.id);
            if ((isSelected && !invertSelection) || (!isSelected && invertSelection))
            {
                toRemove.Add(island);
            }
        }

        // Remove the selected islands and oceans
        foreach (Island island in toRemove)
        {
            // Here you could instantiate a flat land or ocean prefab as needed
            // Example for replacing with flat land:
            GameObject flatLandGO = Instantiate(flatLandPrefab, island.islandObject.transform.position, Quaternion.identity);
            flatLandGO.transform.localScale = new Vector3(island.bounds.size.x, 0.1f, island.bounds.size.z);
            flatLandGO.name = island.islandObject.name + " (Flat Land)";

            // Remove island or ocean from the list and destroy the game object
            RemoveIsland(island);
            Destroy(island.islandObject);
        }
    }


    // Version 1
    public void RemoveSelectedIslands(bool invertSelection)
    {
        List<Island> islandsToRemove = new List<Island>();
        List<int> islandSelection = ActiveIslandSelection;

        // Add the islands to remove based on the current selection
        foreach (Island island in islands)
        {
            if (islandSelection.Contains(island.id) && !invertSelection)
            {
                islandsToRemove.Add(island);
            }
            else if (!islandSelection.Contains(island.id) && invertSelection)
            {
                islandsToRemove.Add(island);
            }
        }

        // Remove the selected islands
        foreach (Island island in islandsToRemove)
        {
            RemoveIsland(island);
        }
    }


    #endregion

    #region Get Island Methods

    // Unique Id 
    public Island GetIslandUID(string ID) // UID: Unique Id
    {
        return islands.Find(island => island.ID == ID);
    }
    // Spawn Id
    public Island GetIslandSID(int id) // SID: Spawn Id
    {
        return islands.Find(island => island.id == id);
    }

    public Island GetIslandByName(string name)
    {
        return islands.Find(island => island.islandName == name);
    }

    public IslandType GetCurrentIslandType(Vector3 playerPosition)
    {
        foreach (Island island in islands)
        {
            if (island.bounds.Contains(playerPosition))
            {
                return island.islandType;
            }
        }
        
        return IslandType.None;
    }
    #endregion
}

