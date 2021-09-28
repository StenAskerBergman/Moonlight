using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WallRunScript : MonoBehaviour
{
    public CharacterController controller;
    bool isGrounded;

    public float jumpHeight = 5f;
    public Transform groundcheck;
    public float grounddistance = 0.4f;
    public LayerMask GroundMask;

    void Update()
    {
        
    }
}
