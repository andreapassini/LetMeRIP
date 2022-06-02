using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpiritForm : PlayerForm
{
    public override void Init(PlayerController characterController)
    {
        base.Init(characterController);

        // declare abilities
        // populate abilites dictionary

        // add ability handleer
        abilityHandler = gameObject.AddComponent<AbilityHandler>();
    }
}
