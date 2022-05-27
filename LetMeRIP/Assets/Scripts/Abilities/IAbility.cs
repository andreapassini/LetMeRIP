using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public interface IAbility
{
    public abstract bool IsReady { get; }

    // setup, performed beofre Perform action, on button down
    public abstract void StartedAction();

    // action, performed right after Started action, there's no wait in betwee, on button down
    public abstract void PerformedAction();

    // end, performed on button up
    public abstract void CancelAction();
}
