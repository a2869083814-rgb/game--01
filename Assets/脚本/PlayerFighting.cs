using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine;

public class PlayerFighting : MonoBehaviour
{
    #region 公开变量
    public WeaponData CurrentWeapon;//当前武器
    public WeaponData WeaponData;//武器数据
    public LayerMask EnemyLayer;//敌人图层
    public Transform AttackPoint;//攻击点
    public WeaponData DefaultWeaponData ;//默认武器数据
    
    #endregion

    #region 私有变量
    private int _comboStep = 0;//连击步骤
    private float _lastAttackTime = 0f;//上次攻击时间
    private Animator _animator;//动画组件
    private PlayerStatusManager _statusManager;//玩家状态管理器
    private PlayerInput _playerInput;//玩家输入组件
    #endregion

    #region unity生命周期方法
    private void Awake()
    {
        _animator = GetComponent<Animator>();//获取动画组件
        _statusManager = GetComponent<PlayerStatusManager>();//获取状态管理器组件
        _playerInput = GetComponent<PlayerInput>();//获取玩家输入组件
    }
    // Start is called before the first frame update
    void Start()
    {
        if(CurrentWeapon == null && DefaultWeaponData != null)
        {
            CurrentWeapon = DefaultWeaponData;//设置默认武器
            Debug.Log("未设置武器，使用默认武器");
        }
    }

    // Update is called once per frame
    void Update()
    {
        //OnAttacking();//跳转攻击状态
    }
    #endregion

    #region 输入处理
    private void OnFire(InputValue Value)
    {
        if (Value.isPressed && _statusManager.TryChangeState(PlayerState.Attacking))
        {
            if (Time.time - _lastAttackTime > CurrentWeapon.ComboResetTime)
            {
                _comboStep = 0;//重置连击步骤
            }//检查连击重置时间
            _animator.SetTrigger("Attack");//播放攻击动画
            Debug.Log("攻击输入触发" +_comboStep);
            _comboStep = (_comboStep + 1) % CurrentWeapon.ComboSteps.Count;//更新连击步骤
            _lastAttackTime = Time.time;//更新上次攻击时间
        }
    }

    //private void OnAttacking()
    //{
    //    if (_playerInput.actions["Fire"].WasPressedThisFrame())
    //    {
    //        WeaponData ActiveWeapon = (CurrentWeapon != null) ? CurrentWeapon : DefaultWeaponData;//获取当前武器数据

    //        if (ActiveWeapon != null && _statusManager.TryChangeState(PlayerState.Attacking))
    //        {
    //            if (ActiveWeapon.ComboSteps.Count > 0)
    //            {
    //                _animator.Play(ActiveWeapon.ComboSteps[_comboStep].AnimTrigger);//播放连击动画
    //            }
    //            else
    //            {
    //                _animator.SetTrigger(ActiveWeapon.ComboSteps[_comboStep].AnimTrigger);//播放攻击动画
    //            }
    //        }
    //    }
    //}
    #endregion

    #region 动画事件
    public void ExecuteHitCheak()
    {
        WeaponData ActiveWeapon = (CurrentWeapon != null) ? CurrentWeapon : DefaultWeaponData;//获取当前武器数据
        if (ActiveWeapon == null)
        {
            Debug.LogError("严重错误：玩家既没有装备武器，也没有默认武器数据！");
        }//错误检查
        float Range = ActiveWeapon.AttackRange;//获取攻击范围
        Collider[] hitEnemies = Physics.OverlapSphere(AttackPoint.position, Range, EnemyLayer);//检测敌人
        foreach (Collider enemy in hitEnemies)
        {
            IDamageable victim = enemy.GetComponent<IDamageable>();//获取敌人IDamageable接口
            if (victim != null)
            {   
                float Damage = ActiveWeapon.BaseDamage;//基础伤害
                victim.TakeDamage(Damage, 5f);//对敌人造成伤害
            }
        }
    }
    #endregion
}
