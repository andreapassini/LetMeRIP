using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpiritFormRebroadcastAnimEvent : MonoBehaviour
{
    //Events to broadcast to abilities, animation's events
    public static event Action<SpiritForm> lightAttack;
    public static event Action<SpiritForm> lightAttackEnd;

    public static event Action<SpiritForm> heavyAttack;
    public static event Action<SpiritForm> heavyAttackEnd;

    public static event Action<SpiritForm> ability1;
    public static event Action<SpiritForm> ability1End;

    public static event Action<SpiritForm> ability2;
    public static event Action<SpiritForm> ability2End;
    public static event Action<SpiritForm> death;

    SpiritForm spiritForm;

    void Start()
    {
        // Get the Berserker from the father
        spiritForm = (SpiritForm)transform.GetComponentInParent(typeof(SpiritForm));
    }

    public void OnLightAttack()
    {
        lightAttack?.Invoke(spiritForm);
    }

    public void OnHeavyAttack()
    {
        heavyAttack?.Invoke(spiritForm);
    }

    public void OnAbility1()
    {
        ability1?.Invoke(spiritForm);
    }

    public void OnAbility2()
    {
        ability2?.Invoke(spiritForm);
    }

    public void OnLightAttackEnd()
    {
        lightAttackEnd?.Invoke(spiritForm);
    }

    public void OnHeavyAttackEnd()
    {
        heavyAttackEnd?.Invoke(spiritForm);
    }

    public void OnAbility1End()
    {
        ability1End?.Invoke(spiritForm);
    }

    public void OnAbility2End()
    {
        ability2End?.Invoke(spiritForm);
    }

    public void OnDeath()
    {
        death?.Invoke(spiritForm);
    }
}
