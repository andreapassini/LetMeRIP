using UnityEngine;
using UnityEngine.InputSystem;
using Cinemachine;
using System.Collections.Generic;

public class CinemachineSwitcher : MonoBehaviour
{
    private static CinemachineSwitcher instance;
    public static CinemachineSwitcher Instance
    {
        get => instance;
    }

    [SerializeField]
    InputAction action;
    [SerializeField]
    private List<CinemachineVirtualCamera> vcams;
    private int currentCamera = 0;  // 0 player
                                    // 1 observer perspective
                                    // 2 observer orthographic
    private void Awake()
    {
        if (instance != null && instance != this) Destroy(this.gameObject);
        else instance = this;
    }

    private void OnEnable()
    {
        action.Enable();
    }

    private void OnDisable()
    {
        action.Disable();
    }

    private void Start()
    {
        action.performed += _ => SwitchState();
    }

    private void SwitchState()
    {
        SetState((currentCamera + 1) % vcams.Count);
    }

    public void SetState(int state)
    {
        currentCamera = state;

        if (currentCamera == 0 || currentCamera ==  2) Camera.main.orthographic = true;
        else Camera.main.orthographic = false;

        vcams.ForEach(vcam => vcam.Priority = 0);
        vcams[currentCamera].Priority = 1;
    }
}
