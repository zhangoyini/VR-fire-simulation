using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Ball_Movement : MonoBehaviour
{
    [SerializeField] private float speed = 1f; // Adjust this to change how fast the ball rolls
    private Rigidbody rb;

    // Start is called before the first frame update
    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    // Update is called once per frame
    void Update()
    {
        if (rb != null)
            rb.velocity = new Vector3(-speed, rb.velocity.y, rb.velocity.z);
    }
}
