using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Laser : MonoBehaviour
{
    public LineRenderer laser;

    void Start()
    {
        Destroy(gameObject, .1f);
    }

    public IEnumerator stopLaser()
	{
        yield return new WaitForSeconds(1f);
        laser.enabled = false;
	}
}
