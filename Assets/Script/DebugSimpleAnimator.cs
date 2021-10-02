using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DebugSimpleAnimator : MonoBehaviour
{
    public bool rotator = false;
    public bool Waver = false;

    public float xAngle, yAngle, zAngle;

    // Update is called once per frame
    void Update()
    {
        if (Waver == true)
        {
            gameObject.transform.Rotate(xAngle, yAngle, zAngle, Space.World);
        }
        if (Waver == true)
        {
            transform.Translate(Vector3.forward * 5f);
            transform.Translate(Vector3.back * 5f);
            //transform.Translate(Vector3.up * Time.deltaTime, Space.World);
        }
    }
}
