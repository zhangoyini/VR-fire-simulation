using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Door_Rotation : MonoBehaviour
{
    [Header("Rotation Setting")]
    [SerializeField] private float openAngle = 90f; 
    [SerializeField] private float openSpeed = 3f;  

    private bool isOpening = false;
    private Quaternion startRotation;
    private Quaternion targetRotation;

    void Start()
    {
        startRotation = transform.localRotation;

        targetRotation = startRotation * Quaternion.Euler(0, openAngle, 0);
    }

    void Update()
    {
        if (isOpening)
        {
            
            transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRotation, Time.deltaTime * openSpeed);

            if (Quaternion.Angle(transform.localRotation, targetRotation) < 0.1f)
            {
                transform.localRotation = targetRotation;
                isOpening = false; 
            }
        }
    }

    
    private void OnTriggerEnter(Collider other)
    {
        // 檢查掂到道門嘅物體，係咪帶有 "Player" 嘅標籤（Tag）
        if (other.CompareTag("Player"))
        {
            isOpening = true;
        }
    }
}
