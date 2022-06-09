using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StunEE : EnemyEffect
{

    private void Awake()
    {
        duration = 1.5f;
    }
    protected override void Start()
    {
        base.Start();
    }

    public void Init(float duration)
    {
        this.duration = duration;
    }

    public override void StartEffect()
    {
        base.StartEffect();
    }

    public override IEnumerator Effect(EnemyForm eform)
    {
        // vfx here
        float previousSwiftness = eform.enemyStats.swiftness;
        eform.enemyStats.swiftness = 0;
        yield return new WaitForSeconds(duration);
        eform.enemyStats.swiftness = previousSwiftness;
        Destroy(this);
    }
}
