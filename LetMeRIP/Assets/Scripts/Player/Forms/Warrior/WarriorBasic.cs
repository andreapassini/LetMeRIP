using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WarriorBasic : PlayerForm
{

    public override void Init(PlayerController characterController)
    {
        base.Init(characterController);

        formModelPrefab.SetActive(true);

        WarriorBasicLightAttack lightAttack = gameObject.AddComponent<WarriorBasicLightAttack>();
        WarriorBasicHeavyAttack heavyAttack = gameObject.AddComponent<WarriorBasicHeavyAttack>();
        WarriorBasicAbility1 ability1 = gameObject.AddComponent<WarriorBasicAbility1>(); 

        abilities[playerInputActions.Player.LightAttack.name] = lightAttack;
        abilities[playerInputActions.Player.HeavyAttack.name] = heavyAttack;
        abilities[playerInputActions.Player.Ability1.name] = ability1;
        
        abilityHandler = gameObject.AddComponent<AbilityHandler>();
        abilityHandler.Init(abilities, characterController);
    }

    private void OnDrawGizmos()
    {
        float alpha = 35f;
        float rad = alpha * Mathf.PI / 180;
        Gizmos.DrawRay(new Ray(transform.position, transform.forward));
        Gizmos.color = Color.yellow;

        Vector3 rbound = new Matrix4x4(
                new Vector4(Mathf.Cos(rad), 0, Mathf.Sin(rad), 0),
                new Vector4(0, 1, 0, 0),
                new Vector4(-Mathf.Sin(rad), 0, Mathf.Cos(rad), 0),
                new Vector4(0, 0, 0, 0)
            ) * transform.forward;
        
        Vector3 lbound = new Matrix4x4(
                new Vector4(Mathf.Cos(rad), 0, -Mathf.Sin(rad), 0),
                new Vector4(0, 1, 0, 0),
                new Vector4(Mathf.Sin(rad), 0, Mathf.Cos(rad), 0),
                Vector4.zero
            ) * transform.forward;

        Gizmos.DrawRay(new Ray(transform.position, rbound));
        Gizmos.DrawRay(new Ray(transform.position, lbound));
    }

}
