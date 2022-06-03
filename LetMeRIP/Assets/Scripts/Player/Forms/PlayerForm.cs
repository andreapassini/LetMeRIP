using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerForm : MonoBehaviour
{
    protected PhotonView photonView;
    public string FormName { get => formName; }
    protected string formName;

    protected PlayerInputActions playerInputActions;
    public AbilityHandler abilityHandler;
    protected Dictionary<string, Ability> abilities;
    protected GameObject formModelPrefab; // contains the model and the animator of the transformation
    protected PlayerController characterController;

    private void Awake()
    {
        photonView = GetComponent<PhotonView>();

        abilities = new Dictionary<string, Ability>();
        playerInputActions = new PlayerInputActions();
        formModelPrefab = transform.Find(GetType().Name.ToString()).gameObject;
    }

    public virtual void Init(PlayerController characterController) 
    {
        this.characterController = characterController;
    }

    public void RemoveComponents()
    {
        if (photonView.IsMine)
        photonView.RPC("RpcRemoveComponents", RpcTarget.All);
    }

    [PunRPC]
    public void RpcRemoveComponents()
    {
        foreach (Ability ability in abilities.Values)
            Destroy(ability);

        if (abilityHandler != null)
            Destroy(abilityHandler);
        formModelPrefab.SetActive(false);
    }
}
