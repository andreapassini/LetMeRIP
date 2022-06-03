using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SampleForm2 : PlayerForm
{
    public override void Init(PlayerController characterController)
    {
        base.Init(characterController);
        photonView.RPC("RpcChangeToSample2Model", RpcTarget.All);

        // abilities declaration
        SampleLightAttack lightAttack = gameObject.AddComponent<SampleLightAttack>();
        SampleHeavyAttack heavyAttack = gameObject.AddComponent<SampleHeavyAttack>();

        // dictionary population
        abilities[playerInputActions.Player.LightAttack.name] = heavyAttack;
        abilities[playerInputActions.Player.HeavyAttack.name] = lightAttack;

        // ability handler initialization
        abilityHandler = gameObject.AddComponent<AbilityHandler>();
        abilityHandler.Init(abilities, characterController);

    }

    // the reason behind this redundancy: many forms are attached to the same gameobject, and all of them inherit from player form, if we 
    // define a general rpc that switches the model, this will be called once for every component inheriting from playerform (activating all of the models at on
    [PunRPC]
    protected void RpcChangeToSample2Model()
    {
        formModelPrefab.SetActive(true);
    }
}
