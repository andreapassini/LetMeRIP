using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SampleForm2 : PlayerForm
{
    private void Start()
    {
        formModelPrefab = Resources.Load<GameObject>("Prefabs/Models/sampleModel2");
    }

    public override void Init(PlayerController characterController)
    {
        base.Init(characterController);
        // add model
        formModelPrefab ??= Resources.Load<GameObject>("Prefabs/Models/sampleModel2");
        Instantiate(formModelPrefab, transform);

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
}
