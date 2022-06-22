using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PlayerController))]
public class PlayerEffect : MonoBehaviourPun
{
    public float Duration { get => duration; }
    protected float duration;
    protected Coroutine effectCoroutine;

    protected virtual void Start()
    {
        /* nothing to see here yet */
    }

    public virtual void StartEffect()
    {
        effectCoroutine = StartCoroutine(Effect(GetComponent<PlayerController>()));
    }

    public virtual IEnumerator Effect(PlayerController characterController) { yield return null; }
}
