using UnityEngine;

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

    public void setPosition(Vector3 _position)
    {
        this.local_position = _position;
    }

    public Vector3 getPosition(BuildingPreview currentBuildingPreview)
    {
        return local_position = currentBuildingPreview.GetBuildingPosition();
    }

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
