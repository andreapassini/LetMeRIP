using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MeshCombiner : MonoBehaviour
{
	public void CombineMeshes()
	{
		// Combine meshes and put the in the same place of the object

		// Save the old place (transform)
		Quaternion oldRot = transform.rotation;
		Vector3 oldPos = transform.position;

		transform.rotation = Quaternion.identity;
		transform.position = Vector3.zero;

		// Get all the mesh filter of the child
		MeshFilter[] filters = GetComponentsInChildren<MeshFilter>();

		Debug.Log(name + " is combining " + filters.Length + " meshes!");

		Mesh finalMesh = new Mesh();

		CombineInstance[] combiners = new CombineInstance[filters.Length];

		// Set up esch filter as a combine instance
		for(int i = 0; i< filters.Length; i++) {
			if (filters[i].transform == transform)
				continue;

			combiners[i].subMeshIndex = 0;
			combiners[i].mesh = filters[i].sharedMesh;
			combiners[i].transform = filters[i].transform.localToWorldMatrix;

		}

		// combine them
		finalMesh.CombineMeshes(combiners);

		// Set our mesh as the combined one
		GetComponent<MeshFilter>().sharedMesh = finalMesh;

		// first rot cause rotating may casue a change in position
		transform.rotation = oldRot;
		transform.position = oldPos;

		// deactivate each child so is light and i can reactive them to do some modification
		for(int i = 0; i<filters.Length; i++) {
			transform.GetChild(i).gameObject.SetActive(false);
		}
	}
}
