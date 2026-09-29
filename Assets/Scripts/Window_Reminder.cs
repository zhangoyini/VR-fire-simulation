using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class Window_Reminder : MonoBehaviour
{
    [SerializeField] private TMP_Text reminder;

    void Start()
    {
        // Optional: Hide the reminder automatically when the game starts
        if (reminder != null)
        {
            reminder.gameObject.SetActive(false);
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Use collision.gameObject to check the tag
        if (collision.gameObject.CompareTag("Player"))
        {
            if (reminder != null)
            {
                reminder.gameObject.SetActive(true);
            }
        }
    }

    // Fires when the player stops touching the solid window
    private void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            if (reminder != null)
            {
                reminder.gameObject.SetActive(false);
            }
        }
    }
}
