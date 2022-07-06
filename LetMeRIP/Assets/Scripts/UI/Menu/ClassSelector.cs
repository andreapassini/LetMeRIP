using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

public enum PlayerClass
{
    WARRIOR = 0,
    MAGE = 1,
    OBSERVER = 2
}

public class ClassSelector
{
    private static ClassSelector _instance = null;
    public static ClassSelector Instance 
    {
        get 
        {
            if( _instance == null )
            {
                _instance = new ClassSelector();
            } 
            return _instance;
        } 
    }

    private PlayerClass selectedClass;
    public PlayerClass SelectedClass 
    {
        get { return _instance.selectedClass; } // warrior by default
    } 

    public void ChangeClass(int newClass)
    {
        selectedClass = (PlayerClass) newClass;
        if (PhotonNetwork.IsConnected)
        {
            PhotonNetwork.LocalPlayer.CustomProperties["class"] = SelectedClass;
        }
    }

}
