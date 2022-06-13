using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class LightDown : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        if (PhotonNetwork.IsMasterClient)
            StartCoroutine(DestroyAfterTime());
    }

    private IEnumerator DestroyAfterTime()
	{
        yield return new WaitForSeconds(1f);

        PhotonNetwork.Destroy(gameObject);

	}
}
