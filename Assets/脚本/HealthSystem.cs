using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class HealthSystem 
{
    #region 公开变量
    public float Health = 100f;//敌人生命值
    public float CurrentHealth;//当前生命值
    #endregion

    public void ReduceHealth(float amount)
    {
        CurrentHealth -= amount;
        CurrentHealth = Mathf.Clamp(CurrentHealth, 0, Health);
    }
}
