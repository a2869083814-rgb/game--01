using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Weapon", menuName = "Combat/WeaponData")]
public class WeaponData : ScriptableObject
{
    public string WeaponName;//武器名称
    public float AttackRange = 1f;//攻击范围
    public float BaseDamage = 1f;//基础攻击力
    public List<AttackAction> ComboSteps;//攻击动作列表
    public float ComboResetTime = 2f;//连击重置时间
}
