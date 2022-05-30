using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpiritForm : PlayerForm
{
    public override void Init(PlayerController characterController)
    {
        base.Init(characterController);
        // add model prefab
        // declare abilities
        // populate abilites dictionary
        // add ability handleer
    }
    #region old spirit form
    //private bool LAready, HAready, Eready, Qready, DashReady;
    //private bool LA, HA, E, Q, Dash, PickUp;

    //public float LA_CD;
    //public float HA_CD;
    //public float E_CD;
    //public float Q_CD;
    //public float Dash_CD;

    //void Start()
    //{

    //}

    //// Update is called once per frame
    //void Update()
    //{
    //    // Light Attack
    //    if (LAready) // & button LA
    //    {
    //        StartCoroutine(LACD());
    //    }

    //    // Heavy Attack
    //    if (HAready) // & button HA
    //    {
    //        StartCoroutine(HACD());
    //    }

    //    // E
    //    if (Eready) // & button E
    //    {
    //        StartCoroutine(ECD());
    //    }

    //    // Q
    //    if (Qready) // & button Q
    //    {
    //        StartCoroutine(QCD());
    //    }

    //    // Dash
    //    if (DashReady) // & button Dash
    //    {
    //        StartCoroutine(DashCD());
    //    }

    //    // Spirit Gauge Sucking
    //    // If on SGpool


    //    // Pickup Body
    //    // if Button PickUp
    //}

    //private void FixedUpdate()
    //{
    //    if (LA)
    //    {
    //        // Act
    //    }

    //    if (HA)
    //    {
    //        // Act
    //    }

    //    if (E)
    //    {
    //        // Act
    //    }

    //    if (Q)
    //    {
    //        // Act
    //    }

    //    if (Dash)
    //    {
    //        // Act
    //    }
    //}

    // Overheal

    #region CoolDown Coroutines
    //private IEnumerator LACD()
    //{
    //    yield return new WaitForSeconds(LA_CD);
    //    LAready = true;
    //}

    //private IEnumerator HACD()
    //{
    //    yield return new WaitForSeconds(HA_CD);
    //    HAready = true;
    //}
    //private IEnumerator ECD()
    //{
    //    yield return new WaitForSeconds(E_CD);
    //    Eready = true;
    //}
    //private IEnumerator QCD()
    //{
    //    yield return new WaitForSeconds(Q_CD);
    //    Qready = true;
    //}

    //private IEnumerator DashCD()
    //{
    //    yield return new WaitForSeconds(Dash_CD);
    //    DashReady = true;
    //}
    #endregion
    #endregion
}
