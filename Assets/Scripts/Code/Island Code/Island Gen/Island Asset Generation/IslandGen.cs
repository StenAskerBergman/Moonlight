using UnityEngine;

public class IslandGen : MonoBehaviour
{

    // Start is called before the first frame update
    void Start()
    {
        IslandFactory islandFactory = new IslandFactory(Biome.Forest);
        ITerrain islandTerrain = islandFactory.CreateTerrain();
        islandTerrain.GenerateMesh();  // Generate the terrain mesh
    }

    // Update is called once per frame
    void Update()
    {

    }
}
