using Photon.Pun;
using UnityEngine;

namespace Networking
{
    public class SpawnPlayers : MonoBehaviour
    {
        public GameObject playerPrefab;

        private void Start()
        {
            Vector3 pos = new Vector3(0, 4, 0);
            PhotonNetwork.Instantiate(playerPrefab.name, pos, Quaternion.identity);
        }
    }
}