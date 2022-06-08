using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SlowEE : EnemyEffect
{
    private float speedMultiplier = .5f;    // multiplied by the enemy's swiftness
                                            // 1 => normal speed
                                            // (0,1) => slowed
                                            // 0 => stunned
                                            // (1, +inf] => speed up
    public float SlowFactor { get => speedMultiplier; set => speedMultiplier = value; }

    private void Awake()
    {
        duration = 2f; // if set in the start it doesn't work apparently ¯\_(?)_/¯
    }

    protected override void Start()
    {
        base.Start();
    }

    public override void StartEffect()
    {
        base.StartEffect();
    }

    public override IEnumerator Effect(EnemyForm eform)
    {
        // vfx here

        float previousSwiftness = eform.enemyStats.swiftness;
        eform.enemyStats.swiftness *= speedMultiplier;
        yield return new WaitForSeconds(duration);
        eform.enemyStats.swiftness = previousSwiftness;
        Destroy(this);
    }
}
