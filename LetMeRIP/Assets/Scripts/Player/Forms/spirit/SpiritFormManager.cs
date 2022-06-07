using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpiritFormManager : FormManager
{
    public override void Init(PlayerController characterController)
    {
        isSpirit = true;
        base.Init(characterController);

        forms.Add(gameObject.AddComponent<SpiritForm>());
        SwitchForm(0); //?
    }
}
