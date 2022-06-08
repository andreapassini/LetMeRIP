using System.Collections;
using UnityEngine;



    public class Test : MonoBehaviour
    {
        public FillingBar healthBar;
    
        void Start()
        {
            StartCoroutine(test());
        }
    
    
        IEnumerator test()
        {
            yield return new WaitForSeconds(2);  

            healthBar.SetMaxValue(100);
            healthBar.SetValue(80);
            yield return new WaitForSeconds(2);  
        
            healthBar.SetValue(50);
            yield return new WaitForSeconds(2);
        
            healthBar.SetValue(20);
        }
    }

