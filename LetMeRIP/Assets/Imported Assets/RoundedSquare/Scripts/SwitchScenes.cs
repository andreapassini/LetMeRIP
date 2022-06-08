/*
SwitchScenes.cs
this script switches scenes...not much to see here.
*/

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Imported_Assets.RoundedSquare.Scripts
{
	public class SwitchScenes : MonoBehaviour 
	{

		public int sceneNum;

		// Use this for initialization
		void Start () 
		{
			int thiSceneNum = SceneManager.GetActiveScene().buildIndex;

			if (sceneNum == thiSceneNum)
			{
				GetComponent<Button>().interactable = false;
			}
		}
	
		// on button press
		public void Press () 
		{

			if (sceneNum < 0)
			{
				Debug.LogError("no negative scenes");
				return;
			}

			if (sceneNum > SceneManager.sceneCountInBuildSettings)
			{
				Debug.LogError("scene doesn't exist");
				return;
			}


			SceneManager.LoadScene(sceneNum);
		}
	}
}
