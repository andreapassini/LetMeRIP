using System.IO;
using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RoomManager : MonoBehaviourPunCallbacks
{
    public static RoomManager Instance;
    [SerializeField] private int gameSceneIndex;


    private void Awake()
    {
        if (Instance) // check if there's already another room manager
        {
            // if there is destroy this cuz it's not needed
            Destroy(gameObject);
            return;
        }

        // make this non-destroyable when scene is switched
        DontDestroyOnLoad(gameObject);
        Instance = this;
    }

    public override void OnEnable()
    {
        base.OnEnable();
        // add my callback to the ones that are called everytime the scene is changed
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // my custom callback
    private void OnSceneLoaded(Scene scene, LoadSceneMode loadSceneMode)
    {
        // if we're not switching to the game scene return
        Debug.Log("Current scene index = " + scene.buildIndex + "\n Game scene index = " + gameSceneIndex);
        if (scene.buildIndex != gameSceneIndex) return;

        PhotonNetwork.Instantiate(Path.Combine("Prefabs", "PlayerManager"), Vector3.zero, Quaternion.identity);
    }
}