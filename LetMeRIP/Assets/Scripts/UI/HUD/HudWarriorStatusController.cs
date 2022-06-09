using System.Collections.Generic;
using UnityEngine;


public class HudWarriorStatusController : HudStatusController
{
    private void Start()
    {
        abilitiesSprites = new Dictionary<Form, Dictionary<EAbility, Sprite>>
        {
            [Form.Spirit] = new Dictionary<EAbility, Sprite>()
            {
                [EAbility.LightAttack] = Resources.Load<Sprite>("Sprites/Abilities/Charge.png"),
                [EAbility.HeavyAttack] = Resources.Load<Sprite>("Sprites/Abilities/Charge.png"),
                [EAbility.Attack1] = Resources.Load<Sprite>("Sprites/Abilities/Charge.png"),
                [EAbility.Attack2] = Resources.Load<Sprite>("Sprites/Abilities/Charge.png"),
                [EAbility.Dash] = Resources.Load<Sprite>("Sprites/Abilities/Charge.png"),
            },
            [Form.Base] = new Dictionary<EAbility, Sprite>()
            {
                [EAbility.LightAttack] = Resources.Load<Sprite>("Sprites/Abilities/Charge.png"),
                [EAbility.HeavyAttack] = Resources.Load<Sprite>("Sprites/Abilities/Charge.png"),
                [EAbility.Attack1] = Resources.Load<Sprite>("Sprites/Abilities/Charge.png"),
                [EAbility.Attack2] = Resources.Load<Sprite>("Sprites/Abilities/Charge.png"),
                [EAbility.Dash] = Resources.Load<Sprite>("Sprites/Abilities/Charge.png"),
            },
            [Form.Trans1] = new Dictionary<EAbility, Sprite>()
            {
                [EAbility.LightAttack] = Resources.Load<Sprite>("Sprites/Abilities/Mage/LightAttack"),
                [EAbility.HeavyAttack] = Resources.Load<Sprite>("Sprites/Abilities/Mage/HeavyAttack"),
                [EAbility.Attack1] = Resources.Load<Sprite>("Sprites/Abilities/Charge"),
                [EAbility.Attack2] = Resources.Load<Sprite>("Sprites/Abilities/Charge"),
                [EAbility.Dash] = Resources.Load<Sprite>("Sprites/Abilities/Charge"),
            },
            [Form.Trans2] = new Dictionary<EAbility, Sprite>()
            {
                [EAbility.LightAttack] = Resources.Load<Sprite>("Sprites/Abilities/Charge.png"),
                [EAbility.HeavyAttack] = Resources.Load<Sprite>("Sprites/Abilities/Charge.png"),
                [EAbility.Attack1] = Resources.Load<Sprite>("Sprites/Abilities/Charge.png"),
                [EAbility.Attack2] = Resources.Load<Sprite>("Sprites/Abilities/Charge.png"),
                [EAbility.Dash] = Resources.Load<Sprite>("Sprites/Abilities/Charge.png"),
            }
        };
        
        Debug.Log("DIC " + abilitiesSprites);

        formsSprites = new Dictionary<Form, Sprite>
        {
            [Form.Spirit] = Resources.Load<Sprite>("Sprites/Abilities/Charge"),
            [Form.Base] = Resources.Load<Sprite>("Sprites/Abilities/Charge"),
            [Form.Trans1] = Resources.Load<Sprite>("Sprites/Abilities/Charge"),
            [Form.Trans2] = Resources.Load<Sprite>("Sprites/Abilities/Charge")
        };
        
        Debug.Log("FORMS " + abilitiesSprites);
        
        formManager.Init(formsSprites, Form.Base);

    }
}