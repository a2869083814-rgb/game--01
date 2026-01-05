using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public class EnemyTest : MonoBehaviour, IDamageable
{
    #region 公开变量
    public HealthSystem EnemyHealth = new HealthSystem();//敌人生命系统

    #endregion

    #region 接口实现
    public void TakeDamage(float amount, float impact)
    {
        EnemyHealth.ReduceHealth(amount);//减少血量
        Debug.Log($"{gameObject.name}的血量剩余:{EnemyHealth.CurrentHealth}");//输出当前血量
        var renderer = GetComponentInChildren<Renderer>();
        if ( renderer != null )
        {
            renderer.material.color = Color.red;//受伤变红
            Invoke("ResetColor", 0.2f);//0.2秒后恢复颜色
        }
        HandleImpact(impact);
    }
    #endregion

    #region 冲击力
    public void HandleImpact(float force)
    {
    transform.Translate(Vector3.back * force * Time.deltaTime);//向后移动
    }
    #endregion
}
