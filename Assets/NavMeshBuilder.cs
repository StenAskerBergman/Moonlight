using UnityEngine;
using UnityEngine.AI;

public class NavMeshBuilder : MonoBehaviour
{
    private NavMeshSurface navMeshSurface;

    private void Awake()
    {
        navMeshSurface = GetComponent<NavMeshSurface>() ?? gameObject.AddComponent<NavMeshSurface>();

        if (navMeshSurface == null)
        {
            Debug.LogError("NavMeshSurface component not found on the GameObject.");
        }
        else
        {
            // Optionally, exclude islands here if they are already known and can be accessed
            ExcludeIslands();
            BakeNavMesh();
        }
    }

    // This method bakes the mesh synchronously
    public void BakeNavMesh()
    {
        if (navMeshSurface != null)
        {
            navMeshSurface.BuildNavMesh();
        }
    }

    // This method can be called to rebake the NavMesh asynchronously
    public void BakeNavMeshAsync()
    {
        if (navMeshSurface != null)
        {
            // The data is not immediately applied to the NavMesh
            NavMeshData navMeshData = new NavMeshData();
            NavMesh.AddNavMeshData(navMeshData);
            // Async operation
            navMeshSurface.UpdateNavMesh(navMeshData);
        }
    }

    // Add Data to the NavMesh asynchronously
    public void AddNavMeshAsync(NavMeshData add_navMeshData)
    {
        if (navMeshSurface != null)
        {
            // The data is not immediately applied to the NavMesh
            add_navMeshData = new NavMeshData();
            NavMesh.AddNavMeshData(add_navMeshData);
            // Async operation
            navMeshSurface.UpdateNavMesh(add_navMeshData);
        }
    }

    // Remove Data to the NavMesh asynchronously
    public void SubNavMeshAsync(NavMeshData sub_navMeshData)
    {
        if (navMeshSurface != null)
        {
            // The data is not immediately applied to the NavMesh
            NavMeshData navMeshData = sub_navMeshData;  // This line can be optimized

            // Add the NavMeshData to the NavMesh and store the instance returned
            NavMeshDataInstance navMeshDataInstance = NavMesh.AddNavMeshData(navMeshData);

            // Use the NavMeshDataInstance to remove the NavMeshData
            NavMesh.RemoveNavMeshData(navMeshDataInstance);

            // Async operation
            navMeshSurface.UpdateNavMesh(sub_navMeshData);
        }
    }

    // This method would exclude all islands from the ocean nav mesh
    public void ExcludeIslands()
    {
        // Find all GameObjects that are islands, possibly by tag or name
        GameObject[] islands = GameObject.FindGameObjectsWithTag("Ground"); // Make sure your islands have this tag

        foreach (GameObject island in islands)
        {
            // Ensure there is a NavMeshModifier component and set it to ignore from build
            var modifier = island.GetComponent<NavMeshModifier>() ?? island.AddComponent<NavMeshModifier>();
            modifier.ignoreFromBuild = true;
        }

        // Assuming you want the changes to take effect immediately
        BakeNavMesh();
    }
}
