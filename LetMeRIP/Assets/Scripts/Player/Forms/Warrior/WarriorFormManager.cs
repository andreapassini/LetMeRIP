using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WarriorFormManager : FormManager
{
    public override void Init(PlayerController characterController)
    {
        isSpirit = false;
        base.Init(characterController);

        forms.Add(gameObject.AddComponent<WarriorBasic>());
        forms.Add(gameObject.AddComponent<Berserker>());
        SwitchForm(0);

        BindAbilities();
    }

    public override void BindAbilities()
    {
        base.BindAbilities();

        if (!photonView.IsMine) return;

        playerInputActions.Player.Transformation1.performed += ctx => SwitchForm(1);
        playerInputActions.Player.Transformation2.performed += ctx => SwitchForm(0);
    }
}
