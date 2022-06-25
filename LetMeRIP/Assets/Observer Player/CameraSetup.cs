using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;
public class CameraSetup : MonoBehaviour
{
    void Start()
    {
        Camera.main.orthographic = false;
        CinemachineBrain brain = Camera.main.gameObject.GetComponent<CinemachineBrain>();
        CinemachineVirtualCamera vc = FindObjectOfType<CinemachineVirtualCamera>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
