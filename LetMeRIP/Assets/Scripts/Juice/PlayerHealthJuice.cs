using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class PlayerHealthJuice : MonoBehaviour
{
    private Animator animator;

    private float health;

    // Start is called before the first frame update
    void Start()
    {
        animator = GetComponent<Animator>();

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

        animator.SetTrigger("damage");
    }
}
