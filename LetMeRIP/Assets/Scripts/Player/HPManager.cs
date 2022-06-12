using Photon.Pun;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HPManager : MonoBehaviourPun
{
    public static event Action<PlayerController> OnPlayerKilled;
    public event Action<HPManager> OnPlayerDamaged;
    public event Action<HPManager> OnPlayerHealed;
    private PlayerStats stats;
    public PlayerStats Stats
    {
        get => stats;
        set
        {
            stats = value;
            health = value.health;
        }
    }

    private PlayerController characterController;
    private bool isDead = false;

    private float health { get => stats.health; set => stats.health = value; }
    private float maxHealth { get => stats.maxHealth; set => stats.maxHealth = value; }
    public float Health { get => stats.health; }
    public float MaxHealth { get => stats.maxHealth; }


    void Start()
    {
        characterController = gameObject.GetComponent<PlayerController>();
    }

    public void TakeDamage(float dmg, Vector3 positionHit)
    {
        string target = photonView.IsMine ? "I" : "OTHER";
        Debug.Log($"{target} Got HIT");

        // Calculate defense reduction
        if (dmg > 0 && photonView.IsMine) 
            health -= dmg - (dmg * (stats.defense * 0.01f));

        if (health <= 0 && photonView.IsMine)
        {
            Debug.Log("calling die");
            photonView.RPC("RpcDie", RpcTarget.All);
        }

        // Take damage Event
        OnPlayerDamaged?.Invoke(this);
    }

    public void Heal(float amount, bool overHeal = false)
    {
        if(amount > 0)
        {
            if (overHeal)
            {
                health += amount;
                stats.maxHealth += amount;
            } else
            {
                health = Mathf.Clamp(health + amount, 0, stats.maxHealth);
            }
        }
        OnPlayerHealed?.Invoke(this);
    }

    public void DecayingHeal(float amount, float timeToDecay)
    {
        if (amount <= 0) return;
        StartCoroutine(DecayingHealCo(amount, timeToDecay));
    }

    private IEnumerator DecayingHealCo(float amount, float timeToDecay)
    {
        Heal(amount, true);
        float timeStep = 0.5f;
        float healthLossPerTick = amount * timeStep/timeToDecay;

        while(timeToDecay > 0)
        {
            health-=healthLossPerTick;
            OnPlayerDamaged?.Invoke(this);
            if (health <= 0) yield break; // prevents Die() to be called multiple times
            yield return new WaitForSeconds(timeStep);
            timeToDecay -= timeStep;
        }
    }

    [PunRPC]
    public void RpcDie()
    {
        if (isDead) return;
        isDead = true;

        Debug.Log("someone died");
        FormManager formManager = characterController.formManager;

        Debug.Log($"someone died {photonView.ViewID}, is mine: {photonView.IsMine}, was out: {formManager.IsOut}");
        Animator animator = GetComponentInChildren<Animator>();
        animator.SetTrigger("Death");


        Collider capsuleCollider = GetComponent<Collider>();
        capsuleCollider.enabled = false;
        characterController.lam.enabled = false;
        characterController.movement.playerInputActions.Player.Movement.Disable();
        formManager.DisableAbilities();

        if (photonView.IsMine) characterController.HPManager.Heal(characterController.HPManager.maxHealth, false);

        if (!formManager.IsSpirit)
        {
            int i = 0;
            Debug.Log(++i);
            GameObject model = formManager.currentForm.formModelPrefab;
            Debug.Log(++i);
            model.GetComponent<PhotonAnimatorView>().enabled = false;
            Debug.Log(++i);
            model.transform.SetParent(transform.parent);

            Debug.Log(++i);
            if (!formManager.IsOut && photonView.IsMine) formManager.ToggleSpiritForm();
            Debug.Log(++i);
            if (photonView.IsMine) PhotonNetwork.Destroy(gameObject);
            Debug.Log(++i);
            OnPlayerKilled?.Invoke(characterController);
            Debug.Log(++i);
        }
        else if (photonView.IsMine)
            PhotonNetwork.Instantiate("Prefabs/PlayerTomb", transform.position, Quaternion.identity);

        
        Debug.Log($"Destroying {name}, may i: {photonView.IsMine}");
        OnPlayerKilled?.Invoke(characterController);
        if(photonView.IsMine) PhotonNetwork.Destroy(gameObject);


        #region fottiti
        //FormManager formManager = characterController.formManager;
        //if (!characterController.formManager.IsSpirit)
        //{
        //    Debug.Log("Body died");
        //    Animator animator = GetComponentInChildren<Animator>();
        //    gameObject.tag = "";
        //    animator.SetTrigger("Death");

        //    if (!formManager.isOut)
        //    {
        //        Debug.Log("Body was in use, spawning spirit");
        //        formManager.ToggleSpiritForm();
        //    }
        //    else
        //    {
        //        Debug.Log("Body was NOT in use");
        //    }
            
        //    formManager.currentForm.formModelPrefab.transform.SetParent(transform.parent);
        //    OnPlayerKilled?.Invoke(this);
        //    PhotonNetwork.Destroy(gameObject);
        //}
        //else
        //{
        //    GetComponent<CapsuleCollider>().enabled = false;
        //    Debug.Log("spirit died");
        //    if (photonView.IsMine) 
        //    {
        //        PhotonNetwork.Instantiate("Prefabs/PlayerTomb", transform.position, Quaternion.identity);
        //        formManager.DisableAbilities();
        //        Heal(maxHealth, false); // restore health for eventual revive
        //        PhotonNetwork.Destroy(gameObject);
        //    }
        //}
        #endregion
    }

    public IEnumerator BuffStats(float str, float dex, float Int, float duration)
	{
        stats.strength *= str;
        stats.dexterity *= dex;
        stats.intelligence *= Int;

        yield return new WaitForSeconds(duration);

        stats.strength /= str;
        stats.dexterity /= dex;
        stats.intelligence /= Int;
    }


}
