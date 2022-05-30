using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SampleFormManager : FormManager
{
    public override void Init(CharacterController characterController)
    {
        base.Init(characterController);

        forms.Add(gameObject.AddComponent<SampleForm1>());
        forms.Add(gameObject.AddComponent<SampleForm2>());

        SwitchForm(1); // possiamo dire di avere sempre lo spirito sullo 0 e la forma base della classe sull'1
        
        if (currentForm != null) BindAbilities();
    }
}
