using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MageFormManager : FormManager
{
    public override void Init(PlayerController characterController)
    {
        isSpirit = false;
        base.Init(characterController);

        forms.Add(gameObject.AddComponent<MageBasic>());
        forms.Add(gameObject.AddComponent<Cleric>());
        SwitchForm(0);

        BindAbilities();
    }

    public override void BindAbilities()
    {
        base.BindAbilities();

        if (!photonView.IsMine) return;

        playerInputActions.Player.Enable();

        playerInputActions.Player.Transformation1.Enable();
        playerInputActions.Player.Transformation2.Enable();


        playerInputActions.Player.Transformation1.performed += ctx => SwitchForm(0);
        playerInputActions.Player.Transformation2.performed += ctx => SwitchForm(1);
    }
}
