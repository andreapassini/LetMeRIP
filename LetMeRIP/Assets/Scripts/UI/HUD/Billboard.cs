using UnityEngine;


public class Billboard : MonoBehaviour
{
    public Transform camera;
    [SerializeField] private HudFillingBar healthBar;


    void Start()
    {
        camera = Camera.main.transform;
        // healthBar = this.GetComponentInChildren<HudFillingBar>();
    }

    void LateUpdate()
    {
        transform.LookAt(transform.position + camera.forward);
    }

    public void Init(EnemyForm enemyForm)
    {
        healthBar.SetMaxValue(enemyForm.enemyStats.maxHealth);
        enemyForm.OnEnemyDamaged += form => healthBar.SetValue(form.enemyStats.health);
    }
}