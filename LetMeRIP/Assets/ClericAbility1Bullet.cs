using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ClericAbility1Bullet : MonoBehaviour
{
    [SerializeField] GameObject healingPool;

    private float healAmount;
    

    public void Init(float healAmount)
    {
        this.healAmount = healAmount;
        Debug.Log($"BULLET HEAL AMOUNT: {healAmount}");
    }

    private void Hit() 
    {
        StartCoroutine(HitWait());
    }

    private IEnumerator HitWait()
    {
        GameObject healingPoolInstance = Instantiate(healingPool, transform.position, transform.rotation) as GameObject;
        yield return new WaitForSeconds(0.1f);
        healingPoolInstance.GetComponent<HealingPool>().Init(healAmount);
    }
}
