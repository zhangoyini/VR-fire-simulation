using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Valve.VR;

public class DummyController : MonoBehaviour
{
    [SerializeField] private GameObject controller;
    private bool insideFlag = false;
    private bool pinchFlag = false;
    private GameObject interactingObject;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {

        if (controller != null)
        {
            transform.position = controller.transform.position;
            transform.rotation = controller.transform.rotation;
        }

        if(SteamVR_Input.GetBooleanAction("GrabPinch").state == true)
        {
            pinchFlag = true;
        }
        else
        {
            pinchFlag = false;
        }

        if(insideFlag == true & pinchFlag == true)
        {
            interactingObject.transform.position = transform.position;
            interactingObject.transform.rotation = transform.rotation;  
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if(other.CompareTag("Door") == true)
        {
            insideFlag = true;
            interactingObject = other.gameObject;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Door") == true)
        {
            insideFlag = false;
            interactingObject = null;
        }
    }
}
