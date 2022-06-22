using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WarriorBasicRebroadcastAnimEvent : MonoBehaviour
{
    //Events to broadcast to abilities, animation's events
    public static event Action<WarriorBasic> lightAttack;
    public static event Action<WarriorBasic> lightAttackEnd;

    public static event Action<WarriorBasic> heavyAttack;
    public static event Action<WarriorBasic> heavyAttackEnd;

    public static event Action<WarriorBasic> ability1;
    public static event Action<WarriorBasic> ability1End;

    public static event Action<WarriorBasic> ability2;
    public static event Action<WarriorBasic> ability2End;

    WarriorBasic warriorBasic;

    // Start is called before the first frame update
    void Start()
    {
        // Get the WarriorBasic Componet from the father
        warriorBasic = (WarriorBasic)transform.GetComponentInParent(typeof(WarriorBasic));
    }

    public void OnLightAttack()
    {
        lightAttack?.Invoke(warriorBasic);
    }

    public void OnHeavyAttack()
    {
        heavyAttack?.Invoke(warriorBasic);
    }

    public void OnAbility1()
    {
        ability1?.Invoke(warriorBasic);
    }

    public void OnAbility2()
    {
        ability2?.Invoke(warriorBasic);
    }

    public void OnLightAttackEnd()
    {
        lightAttackEnd?.Invoke(warriorBasic);
    }

    public void OnHeavyAttackEnd()
    {
        heavyAttackEnd?.Invoke(warriorBasic);
    }

    public void OnAbility1End()
    {
        ability1End?.Invoke(warriorBasic);
    }

    public void OnAbility2End()
    {
        ability2End?.Invoke(warriorBasic);
    }
}
