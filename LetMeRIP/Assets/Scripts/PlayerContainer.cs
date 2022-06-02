using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerContainer : MonoBehaviour
{
    private PhotonView PV;

    private void Start()
    {
        PV = GetComponent<PhotonView>();

        if (!PV.IsMine) Destroy(gameObject.transform.Find("Camera").gameObject);
    }
}
