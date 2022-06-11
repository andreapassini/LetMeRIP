using UnityEngine;


public class Billboard : MonoBehaviour
{
    private Transform camera;
    private HudFillingBar healthBar;


    void Awake()
    {
        camera = Camera.main.transform;
        healthBar = this.GetComponentInChildren<HudFillingBar>();
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

    public void Init(PlayerController pc)
    {
        var hpManager = pc.HPManager;
        var formManager = pc.formManager;

        healthBar.SetMaxValue(pc.formManager.IsSpirit ? pc.spiritStats.maxHealth : pc.bodyStats.maxHealth);
        hpManager.OnPlayerDamaged += manager => healthBar.SetValue(manager.Health);
        hpManager.OnPlayerHealed += manager => healthBar.SetValue(manager.Health);


        MoveBar(formManager);
        formManager.OnFormChanged += MoveBar;
    }


    private void MoveBar(FormManager formManager)
    {
        var newY = formManager.currentForm.GetType().Name switch
        {
            "WarriorBasic" => 110,
            "Berserker" => 200,
            _ => 0
        };
        if (newY is 0) return;
        
        healthBar.transform.transform.localPosition = new Vector3(0, newY, 0);
    }
}