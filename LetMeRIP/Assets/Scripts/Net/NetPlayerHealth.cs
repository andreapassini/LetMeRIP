using UnityEngine;
using UnityEngine.InputSystem;
using Photon.Bolt;

public class NetPlayerHealth : EntityBehaviour<ICustomCubeState>
{
    public int localHealth;
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
        action.performed += _ => TakeDamage(1);
    }

    public override void Attached()
    {
        state.CustomCubeHealth = localHealth;

        state.AddCallback(nameof(ICustomCubeState.CustomCubeHealth), HealthCallback);
    }

    private void HealthCallback()
    {
        localHealth = state.CustomCubeHealth;

        if (localHealth <= 0)
        {
            BoltNetwork.Destroy(gameObject);
        }
    }

    private void Update()
    {
    }

    public void TakeDamage(int dmg)
    {
        Debug.Log("Take Damage: " + dmg);
        state.CustomCubeHealth -= dmg;
    }
}
