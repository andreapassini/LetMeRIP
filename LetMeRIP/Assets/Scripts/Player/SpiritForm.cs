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

        abilityHandler = gameObject.AddComponent<AbilityHandler>();

    }
}
