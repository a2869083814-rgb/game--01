using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 第三人称角色移动控制器（稳定单段跳版本）
/// 特点：
/// - 不允许空中连跳
/// - Jump 动画只播放一次
/// - 落地后可再次起跳
/// - 不会卡 Jump 动画
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class ThirdPersonMove : MonoBehaviour
{
    #region 移动参数设置

    [Header("移动设置")]
    [Tooltip("地面移动速度")]
    public float MoveSpeed = 5f;
    [Tooltip("空中移动控制系数（0-1）")]
    [Range(0f, 1f)]
    public float AirControl = 0.5f;

    #endregion

    #region 旋转参数设置

    [Header("旋转设置")]
    [Tooltip("角色转向平滑时间")]
    public float RotationSmoothTime = 0.1f;
    [Tooltip("锁定目标时转向速度")]
    public float LockRotationSpeed = 6f;

    #endregion

    #region 跳跃与重力参数

    [Header("跳跃与重力设置")]
    [Tooltip("跳跃高度")]
    public float JumpHeight = 2f;
    [Tooltip("重力加速度")]
    public float Gravity = -9.81f;
    [Tooltip("跳跃冷却时间（防止连跳过快）")]
    public float JumpCooldown = 0.2f;
    [Tooltip("跳跃缓冲时间（按下跳跃键后等待落地的最大时间）")]
    public float JumpBufferTime = 0.2f;
    [Tooltip("土狼时间（离开平台边缘后仍可跳跃的时间）")]
    public float CoyoteTime = 0.15f;

    #endregion

    #region 地面检测设置

    [Header("地面检测设置")]
    [Tooltip("地面检测球体半径")]
    public float GroundCheckRadius = 0.35f;
    [Tooltip("地面检测向下偏移量")]
    public float GroundCheckOffset = 0.1f;
    [Tooltip("地面层级")]
    public LayerMask GroundLayer;

    #endregion

    #region 目标锁定设置

    [Header("目标锁定设置")]
    [Tooltip("锁定目标对象")]
    public Transform LockTarget;

    #endregion

    #region 组件引用声明

    // 核心组件
    private CharacterController _characterController;
    private Animator _animator;
    private PlayerStatusManager _playerStatus;

    // 摄像机引用
    private Transform _mainCamera;

    #endregion

    #region 移动状态变量

    // 输入相关
    private Vector2 _moveInput;
    private Vector3 _velocity;
    private Vector3 _moveDirection;

    // 跳跃与地面状态
    private bool _isGrounded;
    private bool _jumpConsumed;        // 本次离地是否已使用跳跃

    // 计时器
    private float _jumpBufferTimer;
    private float _coyoteTimer;
    private float _lastJumpTime;

    // 旋转相关
    private float _rotationVelocity;

    // 常量定义
    private const float INPUT_DEADZONE = 0.1f;          // 输入死区，小于此值忽略
    private const float GROUNDED_VELOCITY_Y = -2f;      // 接地时的垂直速度

    #endregion

    #region 动画参数常量

    // 动画参数名称常量
    private const string ANIM_AXIS_X = "AxisX";
    private const string ANIM_AXIS_Y = "AxisY";
    private const string ANIM_IS_GROUNDED = "IsGrounded";
    private const string ANIM_JUMP = "Jump";

    #endregion

    #region Unity生命周期方法

    /// <summary>
    /// 游戏开始时执行一次，用于初始化组件
    /// </summary>
    private void Awake()
    {
        InitializeComponents();
        InitializeCameraReference();
    }

    /// <summary>
    /// 每帧执行一次，处理角色移动逻辑
    /// </summary>
    private void Update()
    {
        // 步骤1：检测地面状态
        _isGrounded = CheckGrounded();

        // 步骤2：处理各种计时器
        HandleTimers();

        // 步骤3：处理跳跃逻辑
        HandleJump();

        // 步骤4：处理移动输入
        HandleMovement(out Vector3 horizontalMove);

        // 步骤5：处理角色旋转
        HandleRotation();

        // 步骤6：应用最终移动
        ApplyMovement(horizontalMove);

        // 步骤7：更新动画参数
        UpdateAnimator();
    }

    #endregion

    #region 初始化方法

    /// <summary>
    /// 初始化角色所需的所有组件
    /// </summary>
    private void InitializeComponents()
    {
        _characterController = GetComponent<CharacterController>();
        _animator = GetComponent<Animator>();
        _playerStatus = GetComponent<PlayerStatusManager>();
    }

    /// <summary>
    /// 初始化主摄像机引用
    /// </summary>
    private void InitializeCameraReference()
    {
        GameObject cameraObject = GameObject.FindGameObjectWithTag("MainCamera");
        if (cameraObject != null)
        {
            _mainCamera = cameraObject.transform;
        }
        else
        {
            Debug.LogWarning("未找到标签为MainCamera的摄像机，角色移动可能无法正确计算方向");
        }
    }

    #endregion

    #region 计时器处理方法

    /// <summary>
    /// 处理所有与时间相关的状态更新
    /// 包括土狼时间、跳跃缓冲、重力应用等
    /// </summary>
    private void HandleTimers()
    {
        // 更新接地状态计时器
        if (_isGrounded)
        {
            _coyoteTimer = CoyoteTime;
            _jumpConsumed = false;     // 落地后重置跳跃消耗状态

            // 确保角色在地面时保持稳定
            if (_velocity.y < 0)
            {
                _velocity.y = GROUNDED_VELOCITY_Y;
            }
        }

        if (_playerStatus != null && _playerStatus.CurrentState == PlayerState.Jumping)
        {
            _playerStatus.TryChangeState(PlayerState.Idle);
        }// 落地后恢复空闲状态

        if (_animator != null)
        {
            _animator.ResetTrigger(ANIM_JUMP);
        }// 重置跳跃触发器，防止卡住动画

        else
        {
            // 在空中时递减土狼时间
            _coyoteTimer -= Time.deltaTime;
        }

        // 更新跳跃缓冲计时器
        _jumpBufferTimer -= Time.deltaTime;

        // 应用重力加速度
        _velocity.y += Gravity * Time.deltaTime;
    }

    #endregion

    #region 跳跃处理方法

    /// <summary>
    /// 处理跳跃逻辑，判断是否可以跳跃并执行跳跃
    /// </summary>
    private void HandleJump()
    {
        // 检查跳跃条件
        bool canJump =
            _jumpBufferTimer > 0 &&                    // 跳跃缓冲时间内
            _coyoteTimer > 0 &&                        // 土狼时间内
            !_jumpConsumed &&                          // 本次离地未使用跳跃
            Time.time - _lastJumpTime > JumpCooldown && // 跳跃冷却已过
            (_playerStatus == null || _playerStatus.CanJump()); // 状态系统允许

        // 条件不满足时直接返回
        if (!canJump)
            return;

        // 执行跳跃计算
        _velocity.y = Mathf.Sqrt(JumpHeight * -2f * Gravity);

        // 更新跳跃状态
        _jumpConsumed = true;
        _jumpBufferTimer = 0;
        _coyoteTimer = 0;
        _lastJumpTime = Time.time;

        // 触发跳跃动画
        if (_animator != null)
        {
            _animator.SetTrigger(ANIM_JUMP);
        }

        // 更新角色状态
        if (_playerStatus != null)
        {
            _playerStatus.TryChangeState(PlayerState.Jumping);
        }
    }

    #endregion

    #region 移动处理方法

    /// <summary>
    /// 处理移动输入，计算水平移动向量
    /// </summary>
    /// <param name="horizontalMove">输出的水平移动向量</param>
    private void HandleMovement(out Vector3 horizontalMove)
    {
        // 初始化输出参数
        horizontalMove = Vector3.zero;
        _moveDirection = Vector3.zero;

        // 检查输入是否有效
        if (_moveInput.magnitude < INPUT_DEADZONE)
            return;

        // 检查角色状态是否允许移动
        if (_playerStatus != null && !CanMove())
            return;

        // 计算摄像机相对移动方向
        Vector3 inputDirection = new Vector3(_moveInput.x, 0, _moveInput.y).normalized;
        float cameraYaw = _mainCamera != null ? _mainCamera.eulerAngles.y : 0f;

        float targetAngle = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg + cameraYaw;
        _moveDirection = Quaternion.Euler(0, targetAngle, 0) * Vector3.forward;

        // 计算移动速度（空中时降低控制力）
        float currentSpeed = _isGrounded ? MoveSpeed : MoveSpeed * AirControl;
        horizontalMove = _moveDirection * currentSpeed * Time.deltaTime;
    }

    /// <summary>
    /// 应用最终移动向量到角色控制器
    /// </summary>
    /// <param name="horizontalMove">水平移动向量</param>
    private void ApplyMovement(Vector3 horizontalMove)
    {
        // 组合水平移动和垂直速度
        Vector3 finalMovement = horizontalMove + Vector3.up * _velocity.y * Time.deltaTime;

        // 通过CharacterController执行移动
        _characterController.Move(finalMovement);
    }

    #endregion

    #region 旋转处理方法

    /// <summary>
    /// 处理角色旋转逻辑
    /// 根据是否锁定目标使用不同的旋转策略
    /// </summary>
    private void HandleRotation()
    {
        // 锁定目标优先逻辑
        if (LockTarget != null)
        {
            HandleLockedRotation();
            return;
        }

        // 自由移动旋转逻辑
        HandleFreeRotation();
    }

    /// <summary>
    /// 处理锁定目标时的角色旋转
    /// </summary>
    private void HandleLockedRotation()
    {
        // 计算指向锁定目标的方向
        Vector3 directionToTarget = LockTarget.position - transform.position;
        directionToTarget.y = 0;

        // 确保方向有效
        if (directionToTarget.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                Time.deltaTime * LockRotationSpeed
            );
        }
    }

    /// <summary>
    /// 处理自由移动时的角色旋转
    /// </summary>
    private void HandleFreeRotation()
    {
        // 检查是否有有效移动方向
        if (_moveDirection.sqrMagnitude < 0.01f)
            return;

        // 计算目标旋转角度
        float targetAngle = Mathf.Atan2(_moveDirection.x, _moveDirection.z) * Mathf.Rad2Deg;

        // 平滑旋转
        float smoothedAngle = Mathf.SmoothDampAngle(
            transform.eulerAngles.y,
            targetAngle,
            ref _rotationVelocity,
            RotationSmoothTime
        );

        // 应用旋转
        transform.rotation = Quaternion.Euler(0, smoothedAngle, 0);
    }

    #endregion

    #region 状态检测方法

    /// <summary>
    /// 检测角色是否接触地面
    /// 使用球体检测法，从角色底部向下检测
    /// </summary>
    /// <returns>是否接触地面</returns>
    private bool CheckGrounded()
    {
        // 计算检测球体位置
        Vector3 spherePosition = transform.position +
            Vector3.down * (_characterController.height / 2 + GroundCheckOffset);

        // 执行球体检测
        return Physics.CheckSphere(spherePosition, GroundCheckRadius, GroundLayer);
    }

    /// <summary>
    /// 检查角色当前状态是否允许移动
    /// 根据状态机判断是否可以移动
    /// </summary>
    /// <returns>是否允许移动</returns>
    private bool CanMove()
    {
        // 如果没有状态管理器，默认允许移动
        if (_playerStatus == null)
            return true;

        // 根据当前状态判断是否允许移动
        switch (_playerStatus.CurrentState)
        {
            case PlayerState.Attacking:
            case PlayerState.Rolling:
            case PlayerState.TakingDamage:
            case PlayerState.Dead:
                return false;
            default:
                return true;
        }
    }

    #endregion

    #region 动画处理方法

    /// <summary>
    /// 更新动画控制器参数
    /// 将角色状态同步到动画状态机
    /// </summary>
    private void UpdateAnimator()
    {
        // 安全检查
        if (_animator == null)
            return;

        // 更新基础状态参数
        _animator.SetBool(ANIM_IS_GROUNDED, _isGrounded);

        // 更新移动输入参数
        _animator.SetFloat(ANIM_AXIS_X, _moveInput.x);
        _animator.SetFloat(ANIM_AXIS_Y, _moveInput.magnitude);
    }

    #endregion

    #region 输入系统回调方法

    /// <summary>
    /// 输入系统：移动控制回调
    /// 当玩家移动摇杆或WASD时触发
    /// </summary>
    public void OnMove(InputValue inputValue)
    {
        _moveInput = inputValue.Get<Vector2>();
    }

    /// <summary>
    /// 输入系统：跳跃控制回调
    /// 当玩家按下跳跃键时触发
    /// </summary>
    public void OnJump(InputValue inputValue)
    {
        // 只在按键按下时记录跳跃缓冲
        if (inputValue.isPressed)
        {
            _jumpBufferTimer = JumpBufferTime;
        }
    }

    #endregion

    #region 调试辅助方法

    /// <summary>
    /// 在Unity编辑器中绘制调试信息
    /// 仅在选中对象时显示
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        // 确保有CharacterController组件
        CharacterController cc = GetComponent<CharacterController>();
        if (cc == null)
            return;

        // 绘制地面检测球体
        Gizmos.color = _isGrounded ? Color.green : Color.red;
        Vector3 sphereCenter = transform.position +
            Vector3.down * (cc.height / 2 + GroundCheckOffset);
        Gizmos.DrawWireSphere(sphereCenter, GroundCheckRadius);

        // 绘制移动方向
        if (_moveDirection.sqrMagnitude > 0.01f)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(transform.position, _moveDirection * 2f);
        }

        // 绘制速度向量
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position, _velocity * 0.5f);
    }

    #endregion
}