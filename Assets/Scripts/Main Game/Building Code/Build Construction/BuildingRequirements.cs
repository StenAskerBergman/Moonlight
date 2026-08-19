using UnityEngine;

// BuildingProperties.cs

// BuildingProperties.cs handles all the buildings various properties, building stats and 
// current building stats which all needs to be maintained and tracked for the various in 
// game systems in order for the game to properly function. 

public class BuildingRequirements : MonoBehaviour
{
    public BuildingPreview currentBuildingPreview;
    public BuildingData currentBuildingData;
    public Vector3 local_position;

    public void SetRequirements(BuildingPreview bp)
    {
        this.currentBuildingPreview = bp;
        this.currentBuildingData = bp.buildingData;
        // Any other necessary setup for this BuildingRequirement instance
    }

    // Gets the Position of the this.local_position
    public void setPosition(Vector3 _position)
    {
        this.local_position = _position;
    }

    // Gets the Position of the BuildingPreview Class
    public Vector3 getPosition(BuildingPreview currentBuildingPreview)
    {
        return local_position = currentBuildingPreview.GetBuildingPosition();
    }

    /// <summary>
    /// Verify() will call the appropriate IsSatisfied method for each
    /// requirement of type BuildingRequirement in currentBuildingData.BuildingRequirements
    /// </summary>
    /// <returns> If all checks passed, return true, or false if something didn't pass</returns>
    public bool Verify()
    {
        return AreBuildingRequirementsMet(local_position);
    }

    public bool AreBuildingRequirementsMet(Vector3 position)
    {
        foreach (BuildingRequirement req in currentBuildingData.BuildingRequirements)
        {
            if (!req.IsSatisfied()) // This will call the appropriate IsSatisfied method for each requirement.
                return false;
        }

        return true;  // If all checks passed, return true.
    }
}
