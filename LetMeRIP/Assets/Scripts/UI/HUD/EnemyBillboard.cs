public class EnemyBillboard: Billboard
{
    public void Init(EnemyForm enemyForm)
    {
        healthBar.Init(enemyForm.enemyStats.maxHealth, enemyForm.enemyStats.health);
        enemyForm.OnEnemyDamaged += form => healthBar.SetValue(form.enemyStats.health);
    }
    
}