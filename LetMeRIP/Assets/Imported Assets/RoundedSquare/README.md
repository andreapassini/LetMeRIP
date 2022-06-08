RoundedSquare
-------------------------------------
[Asset Store Link](http://u3d.as/HLg)  
© 2017 Justin Garza

PLEASE LEAVE A REVIEW OR RATE THE PACKAGE IF YOU FIND IT USEFUL!
Enjoy! :)

Contact  
-------------------------------------
Questions, suggestions, help needed?  
Contact me at:  
Email: jgarza9788@gmail.com  
Cell: 1-818-251-0647  
Contact Info: [justingarza.net/contact](http://justingarza.net/contact/)
  
Description/Features
-------------------------------------
This is a Shader that will round the corners of square sprites, and UI Images.* Round all corners or some.
* Invert rounding.
* Round corners of rectangles and squares.
 
Terms of Use
-------------------------------------
You are free to add this asset to any game you’d like
However:  
please put my name in the credits, or in the special thanks section. :)  
please do not re-distribute.  

Table of Contents 
-------------------------------------
1. Settings
2. Examples
3. Scripts 
	* CreateGrid.cs
	* MaterialManager.cs
	* SwitchScenes.cs
	* TileManager.cs
4. Note about Scene2 


Settings 
-------------------------------------
**PixelSnap:**  
Will snap pixels, (great for pixel art)

**_Color:**  (seen as "Main Color")  
This is the tint color.

**_Radius:**  
this controls the roundness of the corners.

**_scale:**  
if your square has been scaled (to be a rectangle) you'll want to change these values to reflect the same scaling as the object.

**_TR:**  (seen as "_TopRightCorner")  
Controls if the top right corner should be rounded.

**_BR:**  (seen as "_BottomRightCorner") 
Controls if the bottom right corner should be rounded.

**_BL:**  (seen as "_BottomLeftCorner") 
Controls if the bottom left corner should be rounded.

**_TL:**  (seen as "_TopLeftCorner") 
Controls if the top left corner should be rounded.

**_Invert:**  
inverts what pixels are shown/hidden.

![Imgur](http://i.imgur.com/IpgZbVCm.png)

Examples 
-------------------------------------
Below is examples of code to change the settings of the shader at runtime.
(see MaterialManager.cs and TileManager.cs for more complete code)

~~~cs  

//use a variable to convert the bool to int 
int i = 0;

//convert bool to int
i = (TopLeftToggle.isOn)?1:0;

//set the Top Left to true/false
roundedSquare.SetInt("_TL",i); 

//convert bool to int
i = (TopRightToggle.isOn)?1:0;

//set the Top Right to true/false
roundedSquare.SetInt("_TR",i); 

//convert bool to int
i = (BottomRightToggle.isOn)?1:0;

//set the Bottom Right to true/false
roundedSquare.SetInt("_BR",i); 

//convert bool to int
i = (BottomLeftToggle.isOn)?1:0;

//set the Bottom Left to true/false
roundedSquare.SetInt("_BL",i); 

//set the Radius value
roundedSquare.SetFloat("_Radius",RoundnessSlider.value);  

//convert bool to int
i = (InvertToggle.isOn)?1:0;

//set the invert to true/false
roundedSquare.SetInt("_Invert",i); 
        
~~~

  
Scripts
-------------------------------------
This is list of the scripts that are included with a breif description of what they do.

**CreateGrid.cs**  
An editor only script that easily creates a grid of objects

**MaterialManager.cs**  
This script updates the material in the Scene1.

**SwitchScenes.cs**  
this script switches scenes...not much to see here.

**TileManager.cs**  
This script manages the tiles and rounds the corners based on the tiles around it. (see Scene2)

Note about Scene2 
-------------------------------------
1. Open Scene2 in the editor.  
2. Select a few Tiles.
3. Check/uncheck the isActive bool in the TileManager.cs
4. Play the Scene


![Imgur](http://i.imgur.com/d9D1SeUm.gifv)
