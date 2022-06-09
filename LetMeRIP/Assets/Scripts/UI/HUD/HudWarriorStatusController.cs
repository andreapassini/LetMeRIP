using System.Collections.Generic;
using UnityEngine;


public class HudWarriorStatusController : HudStatusController
{
    /*
     * Load all the sprites needed for the current player's class
     */
    private new void Awake()
    {
        base.Awake();    
        
        //holds the sprites of the abilities of each form
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
        
        //Holds the sprites for all the forms
        formsSprites = new Dictionary<Form, Sprite>
        {
            [Form.Spirit] = Resources.Load<Sprite>("Sprites/Abilities/Charge"),
            [Form.Base] = Resources.Load<Sprite>("Sprites/Abilities/Charge"),
            [Form.Trans1] = Resources.Load<Sprite>("Sprites/Abilities/Charge"),
            [Form.Trans2] = Resources.Load<Sprite>("Sprites/Abilities/Charge")
        };
    }
}