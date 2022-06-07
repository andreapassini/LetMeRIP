using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WarriorFormManager : FormManager
{
    public override void Init(PlayerController characterController)
    {
        base.Init(characterController);

        forms.Add(gameObject.AddComponent<WarriorBasic>());
        SwitchForm(0);

        BindAbilities();
    }
}
