using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class DroneEngine : MonoBehaviour
{
    public float rotationSpeed = 100f; // This is for propeller spinning speed.
    public float engineRotationSpeed = 5f; // This is the speed at which the engine will slerp to the drone's direction.
    public AudioClip propellerSound;
    public GameObject propellerObject; // Reference to the propeller GameObject.

    private AudioSource audioSource;
    private bool isMovingForward = false;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
    }

    void Update()
    {
        Rigidbody parentRigidbody = transform.parent.GetComponent<Rigidbody>();
        isMovingForward = parentRigidbody != null && parentRigidbody.velocity.magnitude > 0f;

        // Rotate the engine in the direction of the drone's flight.
        RotateEngine(parentRigidbody);

        // Spin the propeller.
        RotatePropeller();

        // Play the propeller sound if the drone is moving forward.
        if (isMovingForward && !audioSource.isPlaying)
        {
            audioSource.PlayOneShot(propellerSound);
        }
    }

    void RotateEngine(Rigidbody parentRigidbody)
    {
        if (parentRigidbody != null && parentRigidbody.velocity.magnitude > 0f)
        {
            Vector3 flightDirection = parentRigidbody.velocity.normalized;
            Quaternion targetRotation = Quaternion.LookRotation(flightDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * engineRotationSpeed);
        }
    }

    void RotatePropeller()
    {
        if (propellerObject != null)
        {
            float rotationAngle = rotationSpeed * Time.deltaTime;
            propellerObject.transform.Rotate(Vector3.up, rotationAngle);
        }
    }
}
