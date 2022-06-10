using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Utilities : MonoBehaviour
{
    private static GameObject areaIndicator;
    private void Awake()
    {
        areaIndicator = Resources.Load<GameObject>("Prefabs/AreaIndicator");
    }

    public static void SpawnHitSphere(float attackRange, Vector3 position, float timeToLive)
    {
        GameObject hitSphere = Instantiate(areaIndicator, position, Quaternion.identity);
        hitSphere.transform.localScale = new Vector3(attackRange, attackRange, attackRange);
        Destroy(hitSphere, timeToLive);
    }

    public static float DegToRad(float deg) => deg * 0.01745f;
}
