using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpiritForm : PlayerForm
{
    public override void Init(PlayerController characterController)
    {
        base.Init(characterController);
        // add model prefab
        formModelPrefab ??= Resources.Load<GameObject>("Prefabs/Models/Spirit");
        Instantiate(formModelPrefab, transform);

        // declare abilities
        // populate abilites dictionary

        // add ability handleer
        abilityHandler = gameObject.AddComponent<AbilityHandler>();
    }
}
