/*
CreateGrid.cs
An editor only script that easily creates a grid of objects

*/

//only works in editor
#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;


//execute while in EditMode
namespace Imported_Assets.RoundedSquare.Scripts
{
    [ExecuteInEditMode]
    public class CreateGrid : MonoBehaviour {


        //pseudo-button to trigger the creation of the grid
        public bool Run = false; 

        //the prefab the grid will be made out of
        public GameObject item;

        //the Object that will be the parent of all the items 
        private GameObject IParent;

        //the y position of the grid
        public float y = 0.5f;

        //half the with of the completed grid
        private float halfWidth;

        //how much space between items
        public float spacing = 0f;

        //the count of rows and columns (will be a square)
        public int RowColumn = 30;

        //the item name
        public string itemName;
	
        // Update is called once per frame
        void Update () 
        {

            //when run is true
            if (Run)
            {
                //calc halfwidth
                halfWidth = ((float)RowColumn * spacing)/2f;

                //create the parent GameObject
                IParent = new GameObject("iParent");
                IParent.transform.position = Vector3.zero;

                //loop for x
                for (int x =0; x < RowColumn ;x++)
                {
                    //loop for z
                    for (int z =0; z < RowColumn ;z++)
                    {
                        //new gameObject
                        GameObject I = PrefabUtility.InstantiatePrefab(item) as GameObject;

                        //move to the correct position
                        I.transform.position = new Vector3(-halfWidth + (x * spacing),-halfWidth + (z * spacing), y);
                        //rename
                        I.name = itemName+x.ToString()+"x"+z.ToString();

                        //set parent
                        I.transform.parent = IParent.transform;
                    }  
                }

                //don't run again
                Run = false;
            }
        }
    }
}
#endif
