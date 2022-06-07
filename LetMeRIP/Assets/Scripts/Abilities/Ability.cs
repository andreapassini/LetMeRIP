using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using UnityEngine.InputSystem;

public class Ability : MonoBehaviourPun
{
    protected float cooldown;
    public float SPCost = 0f;
    protected bool isReady = true;
    public virtual bool IsReady { get => isReady; }
    protected PlayerController characterController;
    // ability instance startup, treat it like an OnEnable, useful to retrieve components instantiated at runtime
    public virtual void Init() { }
    public virtual void Init(PlayerController characterController) => this.characterController = characterController;

    // setup, performed before Perform action, on button down
    // does something and sets isReady to false when it can start successfully
    public virtual void StartedAction() { }

    // action, performed right after Started action, there's no wait in between, on button down
    // proper candidate to start cooldown if it is not a charged ability
    public virtual void PerformedAction() { }

    // end, performed on button up
    // proper candidate to start cooldown if it's a charge ability
    public virtual void CancelAction() { }

    protected IEnumerator Cooldown()
    {
        yield return new WaitForSeconds(cooldown);
        isReady = true;
    }
}
