using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SampleFormManager : FormManager
{
    public override void Init(PlayerController characterController)
    {
        base.Init(characterController);

        forms.Add(gameObject.AddComponent<SampleForm1>());
        forms.Add(gameObject.AddComponent<SampleForm2>());

        SwitchForm(0); // possiamo dire di avere sempre lo spirito sullo 0 e la forma base della classe sull'1
        
        if (currentForm != null) BindAbilities();
    }

    protected override void BindAbilities()
    {
        base.BindAbilities();
        playerInputActions.Player.Transformation1.performed += ctx => SwitchForm(1);
        playerInputActions.Player.Transformation2.performed += ctx => SwitchForm(2);
    }
}
