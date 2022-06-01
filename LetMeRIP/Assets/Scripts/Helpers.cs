using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Helpers
{
	// To change input to an isometric view
	private static Matrix4x4 isoMatrix = Matrix4x4.Rotate(Quaternion.Euler(0, 45, 0));

	// To return the skewed vector 3
	// (Used the keyword "this" because it's an extension method
	public static Vector3 ToIso(this Vector3 input) => isoMatrix.MultiplyPoint3x4(input);

	public static Vector3 RandomNavmeshLocation(float radius, Vector3 position)
	{
		while (true) {
			Vector3 randomDirection = UnityEngine.Random.insideUnitSphere * radius;
			randomDirection += position;
			UnityEngine.AI.NavMeshHit hit;

			if (UnityEngine.AI.NavMesh.SamplePosition(randomDirection, out hit, radius, 1)) {
				return hit.position;
			}
		}
	}

}
