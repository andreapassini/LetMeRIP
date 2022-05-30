using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SampleForm1 : PlayerForm
{
    private void Start()
    {
        formModelPrefab = Resources.Load<GameObject>("Prefabs/Models/sampleModel1");
    }

    public override void Init(PlayerController characterController)
    {
        base.Init(characterController);
        // add model
        formModelPrefab ??= Resources.Load<GameObject>("Prefabs/Models/sampleModel1");
        Instantiate(formModelPrefab, transform); // let this be first, the following istruction may look for something that this instance might have

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
