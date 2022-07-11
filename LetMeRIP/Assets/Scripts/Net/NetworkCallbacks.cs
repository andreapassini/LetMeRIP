using UnityEngine;
using Photon.Bolt;

public class NetworkCallbacks : GlobalEventListener
{
    public GameObject cube;

    public override void SceneLoadLocalDone(string scene, IProtocolToken token)
    {
        var spawnPos = new Vector3(Random.Range(-5, 5), 0, Random.Range(-5, 5)) ;

        BoltNetwork.Instantiate(cube, spawnPos, Quaternion.identity) ;
    }
}
