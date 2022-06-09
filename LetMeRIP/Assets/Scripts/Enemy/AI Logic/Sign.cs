using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

[RequireComponent(typeof(SphereCollider))]
public class Sign : EnemyForm
{
	public static event Action<EnemyForm> OnSignBroken;

	public override void TakeDamage(float dmg)
	{
		base.TakeDamage(dmg);
	}

	public override void Die()
	{
		base.Die();

		OnSignBroken?.Invoke(this);
	}
}
