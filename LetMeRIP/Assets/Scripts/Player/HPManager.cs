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
    public PlayerController.Stats stats;

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
        if (PhotonNetwork.IsMasterClient && dmg > 0)
            photonView.RPC("RpcTakeDamage", RpcTarget.All, dmg, positionHit);
    }

    public void Heal(float amount, bool overHeal = false)
    {
        if (PhotonNetwork.IsMasterClient && amount > 0)
            photonView.RPC("RpcHeal", RpcTarget.All, amount, overHeal);
    }

    public void DecayingHeal(float amount, float timeToDecay)
    {
        if (PhotonNetwork.IsMasterClient && amount > 0)
            photonView.RPC("RpcDecayingHeal", RpcTarget.All, amount, timeToDecay);
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
    private void RpcDecayingHeal(float amount, float timeToDecay)
    {
        StartCoroutine(DecayingHealCo(amount, timeToDecay));

    }

    [PunRPC]
    private void RpcHeal(float amount, bool overHeal = false)
    {
        if (amount > 0)
        {
            if (overHeal)
            {
                health += amount;
                stats.maxHealth += amount;
            }
            else
            {
                health = Mathf.Clamp(health + amount, 0, stats.maxHealth);
            }
        }
        OnPlayerHealed?.Invoke(this);
    }

    [PunRPC]
    private void RpcTakeDamage(float dmg, Vector3 positionHit)
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

    [PunRPC]
    private void RpcDie()
    {
        if (isDead) return;
        isDead = true;

        Debug.Log("someone died");
        OnPlayerKilled?.Invoke(characterController);
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
            GameObject model = formManager.currentForm.formModelPrefab;
            model.GetComponent<PhotonAnimatorView>().enabled = false;
            model.transform.SetParent(transform.parent);

            if (!formManager.IsOut && photonView.IsMine) formManager.ToggleSpiritForm();
            if (photonView.IsMine) PhotonNetwork.Destroy(gameObject);
        }
        else if (photonView.IsMine)
            PhotonNetwork.Instantiate("Prefabs/PlayerTomb", transform.position, Quaternion.identity);

        
        Debug.Log($"Destroying {name}, may i: {photonView.IsMine}");
        OnPlayerKilled?.Invoke(characterController);
        if (photonView.IsMine)
        {
            characterController.formManager.UnbindAbilities();
            characterController.movement.playerInputActions.Player.Movement.Disable();
            Debug.Log($"Destroying {name}");
            PhotonNetwork.Destroy(gameObject);
        }
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
