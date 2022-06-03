using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpiritForm : PlayerForm
{
    public override void Init(PlayerController characterController)
    {
        base.Init(characterController);
        photonView.RPC("RpcChangeToSpiritModel", RpcTarget.All);

        // declare abilities
        // populate abilites dictionary

        // add ability handleer
        abilityHandler = gameObject.AddComponent<AbilityHandler>();
    }

    // the reason behind this redundancy: many forms are attached to the same gameobject, and all of them inherit from player form, if we 
    // define a general rpc that switches the model, this will be called once for every component inheriting from playerform (activating all of the models at once)
    // the solution is having rpc with different names, which kinda sucks
    [PunRPC]
    protected void RpcChangeToSpiritModel()
    {
        formModelPrefab.SetActive(true);
    }
}
