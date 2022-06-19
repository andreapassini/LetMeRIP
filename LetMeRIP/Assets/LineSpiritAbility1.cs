using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LineSpiritAbility1 : MonoBehaviour
{
    [SerializeField] private LineController lr;
    [SerializeField] private Transform[] points;

    public void Init()
    {
        lr.SetUpLine(points);
    }
}
