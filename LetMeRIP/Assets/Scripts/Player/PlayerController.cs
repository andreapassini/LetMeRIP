using Photon.Pun;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    private PhotonView PV;


    private void Start()
    {
        PV = GetComponent<PhotonView>();

        if (!PV.IsMine) Destroy(gameObject.transform.Find("PlayerCamera").gameObject);
    }
}