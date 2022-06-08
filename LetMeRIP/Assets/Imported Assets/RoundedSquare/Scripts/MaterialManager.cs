/*
MaterialManager.cs
This script updates the material in the Scene1.
*/

using UnityEngine;
using UnityEngine.UI;

namespace Imported_Assets.RoundedSquare.Scripts
{
    public class MaterialManager : MonoBehaviour 
    {

        /// <summary>
        /// The rounded square material
        /// </summary>
        public Material roundedSquare;

        /// <summary>
        /// The top left toggle.
        /// </summary>
        public Toggle TopLeftToggle;

        /// <summary>
        /// The top right toggle.
        /// </summary>
        public Toggle TopRightToggle;

        /// <summary>
        /// The bottom left toggle.
        /// </summary>
        public Toggle BottomLeftToggle;

        /// <summary>
        /// The bottom right toggle.
        /// </summary>
        public Toggle BottomRightToggle;

        /// <summary>
        /// The roundness slider.
        /// </summary>
        public Slider RoundnessSlider;

        /// <summary>
        /// The invert toggle.
        /// </summary>
        public Toggle InvertToggle;

        void Start()
        {
            //sync material on start
            UpdateMaterial();
        }

        //this will execute when one of the UI Objecta are changed
        public void UpdateMaterial()
        {
            int i = 0;

            i = (TopLeftToggle.isOn)?1:0;
            roundedSquare.SetInt("_TL",i); //set the Top Left to true/false

            i = (TopRightToggle.isOn)?1:0;
            roundedSquare.SetInt("_TR",i); //set the Top Right to true/false

            i = (BottomRightToggle.isOn)?1:0;
            roundedSquare.SetInt("_BR",i); //set the Bottom Right to true/false

            i = (BottomLeftToggle.isOn)?1:0;
            roundedSquare.SetInt("_BL",i); //set the Bottom Left to true/false

            roundedSquare.SetFloat("_Radius",RoundnessSlider.value); //set the Radius value 

            i = (InvertToggle.isOn)?1:0;
            roundedSquare.SetInt("_Invert",i); //set the invert to true/false
        }



    }
}
