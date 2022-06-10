using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WarriorBasic : PlayerForm
{

    public override void Init(PlayerController characterController)
    {
        base.Init(characterController);

        formModelPrefab.SetActive(true);

        WarriorBasicLightAttack lightAttack = gameObject.AddComponent<WarriorBasicLightAttack>();
        WarriorBasicHeavyAttack heavyAttack = gameObject.AddComponent<WarriorBasicHeavyAttack>();
        WarriorBasicAbility1 ability1 = gameObject.AddComponent<WarriorBasicAbility1>();
        WarriorBasicAbility2 ability2 = gameObject.AddComponent<WarriorBasicAbility2>();

        abilities[playerInputActions.Player.LightAttack.name] = lightAttack;
        abilities[playerInputActions.Player.HeavyAttack.name] = heavyAttack;
        abilities[playerInputActions.Player.Ability1.name] = ability1;
        abilities[playerInputActions.Player.Ability2.name] = ability2;

        abilityHandler = gameObject.AddComponent<AbilityHandler>();
        abilityHandler.Init(abilities, characterController);
    }
}
