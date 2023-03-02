using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMovementScript : MonoBehaviour
{
    public CharacterController controller;
    public float speed = 12f;
    public float RunSpeed = 6f;
    public float gravity = -9.81f;
    Vector3 velocity;
    public bool isGrounded;

    public float jumpHeight = 5f;
    public Transform groundcheck;
    public float grounddistance = 0.4f;
    public LayerMask GroundMask;

    [SerializeField] StepSoundCycle myScript;
    //Cycle myCycle = GetComponent<Cycle>();
    //myCycle.DoSomething();

    /*private void Start()
    {
        myScript = GetComponent<StepSoundCycle>();
    }*/

    void Update()
    {
        isGrounded = Physics.CheckSphere(groundcheck.position, grounddistance, GroundMask);

        if(isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        Vector3 move = transform.right * x + transform.forward * z;

        controller.Move(move * speed * Time.deltaTime);

        velocity.y += gravity * Time.deltaTime;

        controller.Move(velocity * Time.deltaTime);

        if (Input.GetButtonDown("Jump") && isGrounded) 
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        };
        

        // GetKeyDown = play this once
        // GetKey = play every frame (with no delta time stopper)
        if (Input.GetKeyDown(KeyCode.LeftShift))
        {
            speed = RunSpeed + speed;
            Debug.Log("Shift pressed");
        };

        if (Input.GetKeyUp(KeyCode.LeftShift))
        {   
            speed = speed - RunSpeed;
            Debug.Log("Shift Released");
        };

        // SOUND SECTION
        if (1==velocity.z || 1==velocity.x && isGrounded)
        {
            myScript.CycleRandom();
        };
    }
}
