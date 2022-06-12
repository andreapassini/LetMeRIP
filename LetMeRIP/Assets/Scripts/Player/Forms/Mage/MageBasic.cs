using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MageBasic : PlayerForm
{
    

    public override void Init(PlayerController characterController)
    {
        base.Init(characterController);

        formModelPrefab.SetActive(true);

        mageBasicLightAttack lightAttack = gameObject.AddComponent<mageBasicLightAttack>();
        mageBasicHeavyAttack heavyAttack = gameObject.AddComponent<mageBasicHeavyAttack>();
        mageBasicAbility1 ability1 = gameObject.AddComponent<mageBasicAbility1>();
        mageBasicAbility2 ability2 = gameObject.AddComponent<mageBasicAbility2>();

        abilities[playerInputActions.Player.LightAttack.name] = lightAttack;
        abilities[playerInputActions.Player.HeavyAttack.name] = heavyAttack;
        abilities[playerInputActions.Player.Ability1.name] = ability1;
        abilities[playerInputActions.Player.Ability2.name] = ability2;

        abilityHandler = gameObject.AddComponent<AbilityHandler>();
        abilityHandler.Init(abilities, characterController);
    }

	
}
