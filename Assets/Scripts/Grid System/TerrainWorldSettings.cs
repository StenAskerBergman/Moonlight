using UnityEngine;

/// <summary>
/// The world's authored terrain contract: one asset that every chunk in a map generates from.
///
/// WHY THIS IS AN ASSET AND NOT A COMPONENT. Until now the only authored copy of these ~100
/// fields lived on the island prefab, so tuning the world meant editing a prefab and every
/// chunk carried a full duplicate of values that are conceptually global - the ocean datum,
/// the abyss depth, the coastal profile, the land elevation ladder. Two consequences bit
/// repeatedly: a serialized field added later deserialized as zero in the prefab and needed a
/// bespoke dataVersion migration to repair, and settings corrected in a scene were lost
/// outright when the editor was killed mid-session. A project asset is version controlled and
/// survives both.
///
/// WHY CHUNKS GET A COPY RATHER THAN THIS INSTANCE. Generation MUTATES its settings. Validate,
/// MigrateLandLadder, ApplyLegacyIslandTuning and EnforceAuthoritativeHeights all write derived
/// values back into the object they are given - surfaceFlatlandHeight from the water datum,
/// beach and backshore widths from the beach gradient, the whole semantic height ladder. Handing
/// this instance to sixteen chunks would let chunk N's derived state leak into chunk N+1, and
/// because a ScriptableObject is a real asset, an edit-mode generation would write those derived
/// values back onto disk permanently. The template is therefore INPUT ONLY, and
/// <see cref="CreateResolved"/> hands out a private copy that a chunk may freely mutate.
/// </summary>
[CreateAssetMenu(fileName = "Terrain World Settings", menuName = "Moonlight/Terrain World Settings")]
public sealed class TerrainWorldSettings : ScriptableObject
{
    [Tooltip("Authored world terrain contract. Treated as read-only input: generation never " +
             "writes derived values back into this asset.")]
    [SerializeField] private TerrainGenerationSettings template = new TerrainGenerationSettings();

    /// <summary>
    /// Read-only view for inspection and tooling. Callers must NOT mutate this - use
    /// <see cref="CreateResolved"/> for anything that generates.
    /// </summary>
    public TerrainGenerationSettings Template => template;

    /// <summary>
    /// A private, freely-mutable copy of the authored contract for one chunk.
    ///
    /// The copy goes through JsonUtility rather than a hand-written Clone() on purpose: it
    /// round-trips every [SerializeField] member including the private ones, so the dataVersion
    /// and landLadderVersion stamps that gate the one-shot migrations survive the copy. A
    /// hand-written clone would have to be updated in lockstep with every field ever added, and
    /// silently resetting a version stamp re-fires a migration that overwrites authored values -
    /// the exact failure that made ridge widths un-authorable for several passes.
    /// </summary>
    public TerrainGenerationSettings CreateResolved()
    {
        template ??= new TerrainGenerationSettings();
        var resolved = JsonUtility.FromJson<TerrainGenerationSettings>(JsonUtility.ToJson(template));
        return resolved ?? new TerrainGenerationSettings();
    }
}
