using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Bolt;
using UnityEngine.InputSystem;

public class NetShooting : EntityBehaviour<ICustomCubeState>
{
    public Rigidbody bulletPrefab;
    public float speed;
    public GameObject muzzle;

    [SerializeField] private InputAction action;

    private void OnEnable()
    {
        action.Enable();
    }
    private void OnDisable()
    {
        action.Disable();
    }

    private void Start()
    {
        action.performed += _ => IsShooting();
    }

    public override void Attached()
    {
        state.OnShoot = Shoot; // Detect when state.Shoot is triggered
    }

    private void Update()
    {
        
    }

    public void IsShooting()
    {
        if (!entity.IsOwner)
            return;

        state.Shoot();
    }

    public void Shoot()
    {
        Rigidbody bulletClone = Instantiate(bulletPrefab, muzzle.transform.position, transform.rotation);
        
        bulletClone.velocity = transform.TransformDirection(new Vector3(0, 0, speed));
    }
}
