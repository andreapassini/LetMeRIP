using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SampleForm1 : PlayerForm
{
    public override void Init(PlayerController characterController)
    {
        base.Init(characterController);
        photonView.RPC("RpcChangeToSample1Model", RpcTarget.All);
        photonView.RPC("RpcAddSample1Abilities", RpcTarget.All);
    }

    [PunRPC]
    protected void RpcChangeToSample1Model()
    {
        formModelPrefab.SetActive(true);
    }

    [PunRPC]
    protected void RpcAddSample1Abilities()
    {
        // abilities declaration
        SampleLightAttack lightAttack = gameObject.AddComponent<SampleLightAttack>();
        SampleHeavyAttack heavyAttack = gameObject.AddComponent<SampleHeavyAttack>();

        // dictionary population
        abilities[playerInputActions.Player.LightAttack.name] = lightAttack;
        abilities[playerInputActions.Player.HeavyAttack.name] = heavyAttack;

        // ability handler initialization
        abilityHandler = gameObject.AddComponent<AbilityHandler>();
        abilityHandler.Init(abilities, characterController);
    }
}
