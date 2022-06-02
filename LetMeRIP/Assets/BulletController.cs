using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

public class BulletController : MonoBehaviour
{
    
    public PhotonView PV;

    
    // Start is called before the first frame update
    void Start()
    {
        PV = GetComponent<PhotonView>();
        Rigidbody rb = GetComponent<Rigidbody>();


        if (!PV.IsMine) Destroy(rb);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
