using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpiritForm : PlayerForm
{
    public override void Init(PlayerController characterController)
    {
        base.Init(characterController);

        formModelPrefab.SetActive(true);

        SpiritLightAttack la = gameObject.AddComponent<SpiritLightAttack>();
        SpiritHeavyAttack ha = gameObject.AddComponent<SpiritHeavyAttack>();
        SpiritAbility1 a1 = gameObject.AddComponent<SpiritAbility1>();
        SpiritAbility2 a2 = gameObject.AddComponent<SpiritAbility2>();

        abilities[playerInputActions.Player.LightAttack.name] = la;
        abilities[playerInputActions.Player.HeavyAttack.name] = ha;
        abilities[playerInputActions.Player.Ability1.name] = a1;
        abilities[playerInputActions.Player.Ability2.name] = a2;

        abilityHandler = gameObject.AddComponent<AbilityHandler>();
        abilityHandler.Init(abilities, characterController);
    }
}
