using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class AttackAction
{
    public string Attack; //攻击动作名称
    public float DamageMultiplier; //攻击倍率
    public float ImpactForce; //冲击力
    public float ComboWindowStart; //连击窗口开始时间
    public float ComboWindowEnd;//连击窗口结束时间
    public string AnimTrigger;//动画触发器名称
}
