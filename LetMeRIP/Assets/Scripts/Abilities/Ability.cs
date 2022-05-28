using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class Ability : MonoBehaviour
{
    protected float cooldown;
    protected bool isReady = true;
    public virtual bool IsReady { get => isReady; }

    // setup, performed beofre Perform action, on button down
    public virtual void StartedAction() { }

    // action, performed right after Started action, there's no wait in betwee, on button down
    public virtual void PerformedAction() { }

    // end, performed on button up
    public virtual void CancelAction() { }

    protected IEnumerator Cooldown()
    {
        yield return new WaitForSeconds(cooldown);
        isReady = true;
    }
}