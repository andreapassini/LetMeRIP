using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerHealthJuice : MonoBehaviour
{
    private float health;

    // Start is called before the first frame update
    void Start()
    {
        health = 100;
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void TakeDamage(float damageAmount)
    {
        Debug.Log("Got HIT for: " + damageAmount);
        health -= damageAmount;
    }
}
