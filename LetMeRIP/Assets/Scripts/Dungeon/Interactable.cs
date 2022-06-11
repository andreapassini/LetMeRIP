using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Interactable : MonoBehaviourPun
{
    protected virtual void Start() { }

    public virtual void Effect(PlayerController characterController) { }
}
