public class EnemyBillboard: Billboard
{
    public void Init(EnemyForm enemyForm)
    {
        healthBar.SetMaxValue(enemyForm.enemyStats.maxHealth);
        enemyForm.OnEnemyDamaged += form => healthBar.SetValue(form.enemyStats.health);
    }
    
}