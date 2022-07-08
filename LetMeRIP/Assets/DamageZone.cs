using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DamageZone : MonoBehaviour
{
    [SerializeField] private float damage;
    [SerializeField] private float timeOffset;

    private List<PlayerController> players = new List<PlayerController> ();
    private Coroutine damageCoroutine;

    private void OnEnable()
    {
        damageCoroutine = StartCoroutine(Damage());
        HPManager.OnPlayerKilled += pc => { players.Remove(pc); };
    }

    private void OnDisable()
    {
        players.Clear();
        StopCoroutine(damageCoroutine);
    }

    private void OnTriggerEnter(Collider other)
    {
        if(other.TryGetComponent<PlayerController>(out PlayerController pc) && !players.Contains(pc))
        {
            players.Add(pc);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent<PlayerController>(out PlayerController pc))
        {
            players.Remove(pc);
        }
    }

    private IEnumerator Damage()
    {
        for (; ; )
        {
            try
            {
                foreach (PlayerController pc in players) pc.HPManager.TakeDamage(damage, Vector3.zero);
            }
            catch (System.Exception) { }
            yield return new WaitForSeconds(timeOffset);
        }
    }

}

