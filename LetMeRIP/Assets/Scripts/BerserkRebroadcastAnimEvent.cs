using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BerserkRebroadcastAnimEvent : MonoBehaviour
{
    //Events to broadcast to abilities, animation's events
    public static event Action<Berserker> lightAttack;
    public static event Action<Berserker> lightAttackEnd;

    public static event Action<Berserker> heavyAttack;
    public static event Action<Berserker> heavyAttackEnd;

    public static event Action<Berserker> ability1;
    public static event Action<Berserker> ability1End;

    public static event Action<Berserker> ability2;
    public static event Action<Berserker> ability2End;

    Berserker berserker;

    void Start()
    {
        // Get the Berserker from the father
        berserker = (Berserker)transform.GetComponentInParent(typeof(Berserker));
    }

    public void OnLightAttack()
    {
        lightAttack?.Invoke(berserker);
    }

    public void OnHeavyAttack()
    {
        heavyAttack?.Invoke(berserker);
    }

    public void OnAbility1()
    {
        ability1?.Invoke(berserker);
    }

    public void OnAbility2()
    {
        ability2?.Invoke(berserker);
    }

    public void OnLightAttackEnd()
    {
        lightAttackEnd?.Invoke(berserker);
    }

    public void OnHeavyAttackEnd()
    {
        heavyAttackEnd?.Invoke(berserker);
    }

    public void OnAbility1End()
    {
        ability1End?.Invoke(berserker);
    }

    public void OnAbility2End()
    {
        ability2End?.Invoke(berserker);
    }
}
