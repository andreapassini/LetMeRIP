using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/**
 * This will be attached to the enemy that has to suffer from this effect
 */
[RequireComponent(typeof(EnemyForm))]
public class EnemyEffect : MonoBehaviour
{
    public float Duration { get => duration; }
    protected float duration;

    protected virtual void Start()
    {
        /* nothing to see here yet */
    }


    public virtual void StartEffect()
    {
        StartCoroutine(Effect(GetComponent<EnemyForm>()));
    }

    public virtual IEnumerator Effect(EnemyForm eform) { yield return null; }
}
