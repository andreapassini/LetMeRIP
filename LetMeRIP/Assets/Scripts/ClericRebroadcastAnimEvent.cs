using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ClericRebroadcastAnimEvent : MonoBehaviour
{
    //Events to broadcast to abilities, animation's events
    public static event Action<Cleric> lightAttack;
    public static event Action<Cleric> heavyAttack;
    public static event Action<Cleric> ability1;
    public static event Action<Cleric> ability2;

    Cleric cleric;

    void Start()
    {
        // Get the MageBasicComponet from the father
        cleric = (Cleric)transform.GetComponentInParent(typeof(Cleric));

    }

    public void OnLightAttack()
    {
        lightAttack?.Invoke(cleric);
    }

    public void OnHeavyAttack()
    {
        heavyAttack?.Invoke(cleric);
    }

    public void OnAbility1()
    {
        ability1?.Invoke(cleric);
    }

    public void OnAbility2()
    {
        ability2?.Invoke(cleric);
    }
}
