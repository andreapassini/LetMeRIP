using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerForm : MonoBehaviourPun
{
    public string FormName { get => formName; }
    protected string formName;

    protected PlayerInputActions playerInputActions;
    public AbilityHandler abilityHandler;
    protected Dictionary<string, Ability> abilities;
    public GameObject formModelPrefab; // contains the model and the animator of the transformation
    protected PlayerController characterController;

    private void Awake()
    {
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
        foreach (Ability ability in abilities.Values) Destroy(ability);
        if (abilityHandler != null) Destroy(abilityHandler);

        formModelPrefab.SetActive(false);
    }
}
