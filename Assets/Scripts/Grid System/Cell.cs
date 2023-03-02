public class Cell
{
    // Noise Map Variables
    public bool isWater, isLand; // Am I Water? or Am I Land?

    // Sea Terrain
    public bool isShore, isSea; // Water Types
    public bool isCoast, isDeep, isPlateau; // Water Height

    // Land Terrain
    public bool isDesert, isForest, isBeach, isPlain, isRocky; // Land Types
    public bool isGround, isHill, isMountain; // Land Height

    public Cell(bool a) 
    { 
        isWater = a; 
    }
}