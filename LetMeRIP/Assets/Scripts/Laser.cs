using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Laser : MonoBehaviour
{
    public LineRenderer laser;

    void Start()
    {
        laser = transform.GetComponent<LineRenderer>();

        laser.enabled = true;

        StartCoroutine(stopLaser());
    }

    public IEnumerator stopLaser()
	{
        yield return new WaitForSeconds(1f);
        laser.enabled = false;
	}
}
