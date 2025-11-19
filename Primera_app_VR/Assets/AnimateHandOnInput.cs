using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class HandAnimator : MonoBehaviour
{
    public InputActionProperty pinchInput;
    public InputActionProperty gripInput;

    public Animator animator;

    // Update is called once per frame
    void Update()
    {
        float triggerValue = pinchInput.action.ReadValue<float>();
        float gripValue = gripInput.action.ReadValue<float>();

        animator.SetFloat("Trigger", triggerValue);
        animator.SetFloat("Grip", gripValue);

        // Debug.Log("El valor de trigger es: " + triggerValue);
        // Debug.Log("El valor de grip es: " + gripValue);
    }
}