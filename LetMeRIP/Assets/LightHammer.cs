using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LightHammer : MonoBehaviour
{
    [SerializeField]
    private GameObject hitVfx;

    public void HitVfx()
    {
        Debug.Log("HAMMER HIT");
        GameObject vfxInstance = Instantiate(hitVfx, new Vector3(0f, 0f, 2f), Quaternion.identity, transform.parent);
        vfxInstance.transform.localPosition = new Vector3(0, 0, 2f * transform.parent.localScale.x);
        Destroy(vfxInstance, 2f);
    }

    public void DestroyMe() => Destroy(gameObject, 2.5f);
    
}
