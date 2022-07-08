using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MageBasicRebroadcastAnimEvent : MonoBehaviour
{
    //Events to broadcast to abilities, animation's events
    public static event Action<MageBasic> lightAttack;
    public static event Action<MageBasic> heavyAttack;
    public static event Action<MageBasic> ability1;
    public static event Action<MageBasic> ability2;
    public static event Action<MageBasic> death;

    MageBasic mageBasic;

    void Start()
    {
        // Get the MageBasicComponet from the father
        mageBasic = (MageBasic)transform.GetComponentInParent(typeof(MageBasic));
    }

    public void OnLightAttack()
    {
        lightAttack?.Invoke(mageBasic);
    }

    public void OnHeavyAttack()
    {
        heavyAttack?.Invoke(mageBasic);
    }

    public void OnAbility1()
    {
        ability1?.Invoke(mageBasic);
    }

    public void OnAbility2()
    {
        ability2?.Invoke(mageBasic);
    }
}
