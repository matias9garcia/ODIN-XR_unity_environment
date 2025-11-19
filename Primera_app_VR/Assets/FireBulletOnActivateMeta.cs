using System.Collections;
using System.Collections.Generic;
using Oculus.Interaction;
using Oculus.Interaction.Input;
using UnityEngine;

public class FireBulletOnPinch : MonoBehaviour
{
    [SerializeField] private ControllerRef _controllerRef;
    [SerializeField] private GrabInteractor _grabInteractor;
    [SerializeField] private OVRInput.RawButton shootingButton = OVRInput.RawButton.RIndexTrigger;

    [Header("Disparo")]
    public GameObject bullet;
    public Transform spawnPoint;
    public float fireSpeed = 50f;

    [Header("Restricciones")]
    // [SerializeField] private GameObject pistol; // Referencia al objeto que debe estar agarrado

    private GrabInteractable _grabInteractable;
    private bool _isGrabbing = false;

    void Update()
    {
        if (_grabInteractor == null || _controllerRef == null)
            return;

        bool currentlyGrabbing = _grabInteractor.HasSelectedInteractable;

        if (currentlyGrabbing != _isGrabbing)
        {
            _isGrabbing = currentlyGrabbing;

            if (_isGrabbing)
            {
                _grabInteractable = _grabInteractor.SelectedInteractable;
            }
            else
            {
                _grabInteractable = null;
            }
        }

        if (_isGrabbing && IsHoldingPistol())
        {
            OVRInput.Controller activeController = GetOVRController();

            if (OVRInput.GetDown(shootingButton, activeController))
            {
                FireBullet();
            }
        }
    }

    void FireBullet()
    {
        if (bullet == null || spawnPoint == null) return;

        GameObject spawnedBullet = Instantiate(bullet, spawnPoint.position, spawnPoint.rotation);
        Rigidbody rb = spawnedBullet.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.velocity = spawnPoint.forward * fireSpeed;
        }
        Destroy(spawnedBullet, 5f);
    }

    private bool IsHoldingPistol()
    {
        return _grabInteractable != null &&
           _grabInteractable.gameObject.name == "pistol";
    }

    private OVRInput.Controller GetOVRController()
    {
        if (_controllerRef.Handedness == Handedness.Left)
            return OVRInput.Controller.LTouch;
        else if (_controllerRef.Handedness == Handedness.Right)
            return OVRInput.Controller.RTouch;
        else
            return OVRInput.Controller.None;
    }
}
