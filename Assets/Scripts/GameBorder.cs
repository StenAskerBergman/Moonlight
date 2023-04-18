using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameBorder : MonoBehaviour
{
    //public Vector2 borderSize = new Vector2(100, 100);
    public Vector2 _range = new Vector2(100,100);   // IMPORTANT: Map Boarder

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.white;
        Gizmos.DrawWireCube(transform.position, new Vector3(_range.x * 2, 0, _range.y * 2));
    }

    internal bool IsInBounds(Vector3 position)
    {
        return position.x > -_range.x &&
            position.x < _range.x &&
            position.z > -_range.y &&
            position.z < _range.y;
    }

    internal Vector3 GetNearestPointOnBounds(Vector3 position)
    {
        position.x = Mathf.Clamp(position.x, -_range.x, _range.x);
        position.z = Mathf.Clamp(position.z, -_range.y, _range.y);
        return position;
    }
    

}
 