using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Cleric : PlayerForm
{
    public override void Init(PlayerController characterController)
    {
        base.Init(characterController);

        formModelPrefab.SetActive(true);

        ClericLightAttack lightAttack = gameObject.AddComponent<ClericLightAttack>();
        ClericHeavyAttack heavyAttack = gameObject.AddComponent<ClericHeavyAttack>();
        ClericAbility1 ability1 = gameObject.AddComponent<ClericAbility1>();
        ClericAbility2 ability2 = gameObject.AddComponent<ClericAbility2>();

        abilities[playerInputActions.Player.LightAttack.name] = lightAttack;
        abilities[playerInputActions.Player.HeavyAttack.name] = heavyAttack;
        abilities[playerInputActions.Player.Ability1.name] = ability1;
        abilities[playerInputActions.Player.Ability2.name] = ability2;

        abilityHandler = gameObject.AddComponent<AbilityHandler>();
        abilityHandler.Init(abilities, characterController);
    }
}
