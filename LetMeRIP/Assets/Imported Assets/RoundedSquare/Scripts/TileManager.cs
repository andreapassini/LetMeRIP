/*
TileManager.cs
This script manages the tiles and rounds the corners based on the tiles around it. (see Demo1)
*/


using UnityEngine;

namespace Imported_Assets.RoundedSquare.Scripts
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(BoxCollider2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class TileManager : MonoBehaviour {

        /// <summary>
        /// whether this tile is active or not.
        /// </summary>
        public bool isActive = true;

        /*
    these colors are just a visual aid while editing your scene.
    When the scene starts the color will be changed to white and all the needed corners will be rounded.
    */

        /// <summary>
        /// The color of the active.
        /// </summary>
        public Color activeColor;

        /// <summary>
        /// The color of the inactive.
        /// </summary>
        public Color inactiveColor;


        /// <summary>
        /// The material for this object.
        /// </summary>
        private Material thisMaterial;

        /// <summary>
        /// the shared material for all objects that use this material.
        /// </summary>
        private Material allMaterial;

        //hidden Radius Variable
        /// <summary>
        /// The radius for how round the corners should be
        /// </summary>
        public static float Radius = 0.25f;

        /// <summary>
        /// The collision mask to detect other tile.
        /// </summary>
        public LayerMask collisionMask;

        void Awake()
        {
            if (Application.isPlaying)
            {
                if (isActive)
                {
                    //if active and Playing, show sprite, with active color, and enable collider
                    GetComponent<SpriteRenderer>().enabled = true;
                    GetComponent<SpriteRenderer>().color = activeColor;
                    GetComponent<Collider2D>().enabled = true;

                }
                else
                {
                    //if inactive and Playing, show sprite, with active color, and disable collider
                    GetComponent<SpriteRenderer>().enabled = true;
                    GetComponent<SpriteRenderer>().color = activeColor;
                    GetComponent<Collider2D>().enabled = false;
                }

            }
        }

        void Start()
        {

            if (Application.isPlaying)
            {

                //set all the rounded corners with the Radius value
                allMaterial = gameObject.GetComponent<SpriteRenderer>().sharedMaterial;
                allMaterial.SetFloat("_Radius",Radius);


                //get the material for this object
                thisMaterial = gameObject.GetComponent<SpriteRenderer>().material;
//            thisMaterial.SetFloat("_TR",1f);
//            thisMaterial.SetFloat("_BR",1f);
//            thisMaterial.SetFloat("_BL",1f);
//            thisMaterial.SetFloat("_TL",1f);

                //use raycasts to detect other tile around this one
                RaycastHit2D rcT = Physics2D.Raycast(gameObject.transform.position + new Vector3(0f,1f,0f), Vector2.up , 0.1f, collisionMask);
                RaycastHit2D rcTR = Physics2D.Raycast(gameObject.transform.position + new Vector3(1f,1f,0f), Vector2.up , 0.1f, collisionMask);
                RaycastHit2D rcR = Physics2D.Raycast(gameObject.transform.position + new Vector3(1f,0f,0f), Vector2.up , 0.1f, collisionMask);
                RaycastHit2D rcBR = Physics2D.Raycast(gameObject.transform.position + new Vector3(1f,-1f,0f), Vector2.up , 0.1f, collisionMask);
                RaycastHit2D rcB = Physics2D.Raycast(gameObject.transform.position + new Vector3(0f,-1f,0f), Vector2.up , 0.1f, collisionMask);
                RaycastHit2D rcBL = Physics2D.Raycast(gameObject.transform.position + new Vector3(-1f,-1f,0f), Vector2.up , 0.1f, collisionMask);
                RaycastHit2D rcL = Physics2D.Raycast(gameObject.transform.position + new Vector3(-1f,0f,0f), Vector2.up , 0.1f, collisionMask);
                RaycastHit2D rcTL = Physics2D.Raycast(gameObject.transform.position + new Vector3(-1f,1f,0f), Vector2.up , 0.1f, collisionMask);

                //if this tile is active set the corners based on the raycast results
                if (isActive)
                {

                    thisMaterial.SetFloat("_TR",1f);
                    thisMaterial.SetFloat("_BR",1f);
                    thisMaterial.SetFloat("_BL",1f);
                    thisMaterial.SetFloat("_TL",1f);

                    thisMaterial.SetFloat("_Invert",0);

                    if (rcT || rcR )
                    {
                        thisMaterial.SetFloat("_TR",0f);
                    }

                    if (rcR || rcB )
                    {
                        thisMaterial.SetFloat("_BR",0f);
                    }

                    if (rcB || rcL )
                    {
                        thisMaterial.SetFloat("_BL",0f);
                    }

                    if (rcL || rcT )
                    {
                        thisMaterial.SetFloat("_TL",0f);
                    }
                }
                else //if this tile is inactive get invert the shader and set the corners based on the raycast results
                {

                    thisMaterial.SetFloat("_TR",0f);
                    thisMaterial.SetFloat("_BR",0f);
                    thisMaterial.SetFloat("_BL",0f);
                    thisMaterial.SetFloat("_TL",0f);

                    thisMaterial.SetFloat("_Invert",1);

                    if (rcT && rcTR && rcR )
                    {
                        thisMaterial.SetFloat("_TR",1f);
                    }

                    if (rcR && rcBR && rcB )
                    {
                        thisMaterial.SetFloat("_BR",1f);
                    }

                    if (rcB && rcBL && rcL )
                    {
                        thisMaterial.SetFloat("_BL",1f);
                    }

                    if (rcL && rcTL && rcT )
                    {
                        thisMaterial.SetFloat("_TL",1f);
                    }
                }
            }
        }
	
        // Update is called once per frame
        void Update () 
        {

            //while in Editor (and not playing) change the colors to provide a visual aid.
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                if (isActive)
                {
                    GetComponent<SpriteRenderer>().color = activeColor;
                    GetComponent<Collider2D>().enabled = true;
                }
                else
                {
                    GetComponent<SpriteRenderer>().color = inactiveColor;
                    GetComponent<Collider2D>().enabled = true;
                }
            }
#endif
        }

    }
}

