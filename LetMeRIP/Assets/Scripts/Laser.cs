using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Laser : MonoBehaviour
{
    public LineRenderer laser;

    private bool showLaser = false;

    void Start()
    {
        laser = transform.GetComponent<LineRenderer>();

        showLaser = true;
        laser.enabled = true;

        StartCoroutine(stopLaser());
    }

    public IEnumerator stopLaser()
	{
        yield return new WaitForSeconds(.1f);
        laser.enabled = false;
	}
}
