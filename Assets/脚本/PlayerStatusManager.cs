using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;//引入System命名空间以使用Action委托
//玩家状态管理器
public class PlayerStatusManager : MonoBehaviour
{
    #region 公开变量
    
    public PlayerState CurrentState {  get; private set; }//当前状态
    public event Action<PlayerState, PlayerState> OnStateChanged;//状态改变事件
    public float AttackStateTimeout = 2f;//攻击状态超时时间
    public float RollingStateTimeout = 1f;//翻滚状态超时时间
    public float TakingDamageStateTimeout = 0.5f;//受击状态超时时间

    #endregion

    #region 私有变量

    private float _stateTimer = 0f;//状态计时器
    private PlayerState _previousState;//上一个状态
    #endregion

    #region unity生命周期方法
    void Start()
    {
        CurrentState = PlayerState.Idle;//初始状态为空闲
        _previousState = PlayerState.Idle;
        _stateTimer = Time.time;

        Debug.Log($"初始状态: {CurrentState}");
    }

    void Update()
    {
        CheckStateTimeout();
    }
    #endregion

    #region 状态转换方法
    // 修改为public，让其他脚本可以调用
    public bool TryChangeState(PlayerState newState)
    {
        // 1. 如果已经是目标状态，不需要切换
        if (CurrentState == newState)
        {
            Debug.Log("已经是目标状态: " + CurrentState);
            return false;
        }

        // 2. 检查是否允许转换（调用CanTransitionToState）
        if (!CanTransitionToState(newState))
        {
            Debug.Log($"状态转换被拒绝: {CurrentState} -> {newState}");
            return false;
        }

        // 3. 保存旧状态
        _previousState = CurrentState;

        // 4. 退出当前状态
        OnStateExit(_previousState);

        // 5. 更新当前状态
        CurrentState = newState;
        _stateTimer = Time.time;

        // 6. 进入新状态
        OnStateEnter(CurrentState);

        // 7. 触发状态改变事件（注意：使用?.Invoke避免空引用）
        if (OnStateChanged != null)
        {
            OnStateChanged.Invoke(_previousState, CurrentState);
        }

        // 8. 输出日志
        Debug.Log($"状态已更改: {_previousState} -> {CurrentState}");

        return true;
    }

    private bool CanTransitionToState(PlayerState newState)
    {
        if (CurrentState == PlayerState.Dead)
        {
            return false;
        }//死亡状态不能转换到其他状态

        if (newState == PlayerState.Dead)
        {
            return true;
        }//任何状态都可以转换到死亡状态

        if (newState == PlayerState.TakingDamage)
        {
            return true;
        }//任何状态都可以转换到受击状态

        switch (CurrentState)
        {
            case PlayerState.Idle:
                return newState == PlayerState.Running
                    || newState == PlayerState.Jumping
                    || newState == PlayerState.Attacking
                    || newState == PlayerState.Rolling;//空闲状态可以转换到跑步、跳跃、攻击和翻滚状态
            
            case PlayerState.Running:
                return newState == PlayerState.Rolling 
                    || newState == PlayerState.Attacking
                    || newState == PlayerState.Idle
                    || newState == PlayerState.Jumping;//跑步状态可以转换到翻滚、攻击、空闲和跳跃状态
            
            case PlayerState.Jumping:
                return newState == PlayerState.Idle
                    || newState == PlayerState.Running
                    || newState == PlayerState.TakingDamage;//跳跃状态可以转换到空闲、跑步和受击状态

            case PlayerState.Attacking:
                return newState == PlayerState.Idle
                    || newState == PlayerState.TakingDamage;//攻击状态可以转换到空闲和受击状态

            case PlayerState.Rolling:
                return newState == PlayerState.Idle
                    || newState == PlayerState.TakingDamage;//翻滚状态可以转换到空闲和受击状态

            case PlayerState.TakingDamage:
                return newState == PlayerState.Idle;//受击状态可以转换到空闲状态
            default:
                return false;
        }
    }//检查是否可以转换到新状态

    private void OnStateEnter(PlayerState state)
    {
        switch (state)
        {
            case PlayerState.Attacking:
                Debug.Log("进入攻击状态");
                // 可以在这里播放攻击音效
                break;

            case PlayerState.Rolling:
                Debug.Log("进入翻滚状态");
                // 可以在这里启动无敌帧
                break;

            case PlayerState.TakingDamage:
                Debug.Log("进入受击状态");
                // 可以在这里播放受伤动画和音效
                break;
                //在这里处理进入新状态时的逻辑
        }//进入新状态时的处理
    }

    private void OnStateExit(PlayerState state)
    {
        switch (state)
        {
            case PlayerState.Attacking:
                Debug.Log("攻击状态结束");
                // 可以在这里重置攻击连击计数
                break;

            case PlayerState.Rolling:
                Debug.Log("翻滚状态结束");
                // 可以在这里取消无敌状态
                break;

            case PlayerState.TakingDamage:
                Debug.Log("受击状态结束");
                // 可以在这里恢复角色控制
                break;
        }
        //在这里处理退出当前状态时的逻辑
    }//退出当前状态时的处理

    #endregion
    #region 状态辅助管理方法
     private void CheckStateTimeout()
    {
        float duration = GetStateDuration();

        if (CurrentState == PlayerState.Attacking && duration > AttackStateTimeout)
        {
            Debug.Log("攻击状态超时，切换到待机状态");
            TryChangeState(PlayerState.Idle);
        }

        if (CurrentState == PlayerState.Rolling && duration > RollingStateTimeout)
        {
            Debug.Log("翻滚状态超时，切换到待机状态");
            TryChangeState(PlayerState.Idle);
        }

        if (CurrentState == PlayerState.TakingDamage && duration > TakingDamageStateTimeout)
        {
            Debug.Log("受击状态超时，切换到待机状态");
            TryChangeState(PlayerState.Idle);
        }
    }//检查状态持续时间

    public float GetStateDuration()
    {
        return Time.time - _stateTimer;
    }//获取当前状态持续时间

    public PlayerState GetPreviousState()
    {
        return _previousState;
    }//获取上一个状态
    #endregion

    #region 状态查询方法
    public bool CanAttack()
    {
        return !(CurrentState == PlayerState.Attacking 
            || CurrentState == PlayerState.TakingDamage 
            || CurrentState == PlayerState.Dead 
            || CurrentState == PlayerState.Rolling);
    }//检查是否可以攻击

    public bool CanRolling()
    {
        return !(CurrentState == PlayerState.Dead
            || CurrentState == PlayerState.Rolling);
    }

    public bool CanJump()
    {
        // 死亡状态不能跳跃
        if (CurrentState == PlayerState.Dead)
            return false;

        // 根据游戏设计，哪些状态下允许跳跃
        switch (CurrentState)
        {
            case PlayerState.Idle:// 待机状态允许跳跃
                return true;
            case PlayerState.Running:
                return true;// 可以跳跃
            case PlayerState.Jumping:
                return false;// 已经在跳跃中不能再次跳跃
            case PlayerState.Attacking:
                return GetStateDuration() > 0.3f; // 攻击后半段允许跳跃
            case PlayerState.Rolling:
                return GetStateDuration() > 0.3f; // 翻滚后半段允许跳跃
            case PlayerState.TakingDamage:
                // 受击中不能跳跃
                return false;
            default:
                return false;
        }
    }
    #endregion

        #region  调试方法
    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 200, 300));
        GUILayout.Label($"当前状态: {CurrentState}");
        GUILayout.Label($"上一个状态: {_previousState}");
        GUILayout.Label($"状态持续时间: {GetStateDuration():F2} 秒");
        if (GUILayout.Button("切换到待机状态"))
        {
            TryChangeState(PlayerState.Idle);
        }
        if (GUILayout.Button("切换到跑步状态"))
        {
            TryChangeState(PlayerState.Running);
        }
        if (GUILayout.Button("切换到跳跃状态"))
        {
            TryChangeState(PlayerState.Jumping);
        }
        if (GUILayout.Button("切换到攻击状态"))
        {
            TryChangeState(PlayerState.Attacking);
        }
        if (GUILayout.Button("切换到受伤状态"))
        {
            TryChangeState(PlayerState.TakingDamage);
        }
        if (GUILayout.Button("切换到翻滚状态"))
        {
            TryChangeState(PlayerState.Rolling);
        }
        if (GUILayout.Button("切换到死亡状态"))
        {
            TryChangeState(PlayerState.Dead);
        }
        GUILayout.EndArea();
    }
    #endregion
}
