using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonMove : MonoBehaviour
{
    // 锁定目标：当有目标时，角色会面向并跟踪该目标
    public Transform LockTarget;

    // 角色控制器组件：用于处理角色移动和碰撞
    private CharacterController _controller;

    // 主摄像机：用于基于摄像机的移动计算
    private GameObject _mainCamera;

    // 角色速度：包含水平和垂直速度，用于重力计算
    private Vector3 _velocity;

    // 重力加速度：控制角色下落速度，负值表示向下
    public float gravity = -9.81f;

    // 移动速度：角色水平移动的速度
    public float moveSpeed = 5f;

    // 初始化函数：在游戏开始时调用一次
    void Start()
    {
        // 如果没有指定主摄像机，通过标签找到场景中的主摄像机
        if (_mainCamera == null)
        {
            _mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
        }

        // 获取角色控制器组件，用于移动和碰撞检测
        _controller = GetComponent<CharacterController>();

        // 获取动画控制器组件，用于控制角色动画
        _animator = GetComponent<Animator>();
    }

    // 动画控制器：控制角色的动画状态
    Animator _animator;

    // 目标旋转角度：角色需要旋转到的目标角度
    float _targetRot = 0.0f;

    // 旋转平滑时间：控制旋转过渡的平滑程度，值越大旋转越慢越平滑
    public float rotationSmoothTime = 0.1f;

    // 旋转速度：内部变量，用于平滑旋转计算
    float _rotationVelocity;

    // 每帧更新函数：游戏每一帧都会调用
    void Update()
    {
        // ============ 重力系统（始终应用） ============
        // 检测角色是否在地面上且下落速度小于0
        // 如果是，给一个小的向下力确保角色稳定在地面
        if (_controller.isGrounded && _velocity.y < 0)
        {
            _velocity.y = -2f;
        }

        // 应用重力：根据重力加速度更新垂直速度
        _velocity.y += gravity * Time.deltaTime;

        // ============ 根据锁定状态调用不同移动模式 ============
        // 如果没有锁定目标，使用自由移动模式
        if (LockTarget == null)
        {
            Freemove();
        }
        // 如果有锁定目标，使用锁定目标移动模式
        else
        {
            Lockmove();
        }

        // ============ 始终应用重力移动（无论哪种模式） ============
        // 确保重力在任何情况下都起作用
        ApplyGravityMovement();
    }

    // 应用重力移动函数：专门处理垂直方向的重力移动
    void ApplyGravityMovement()
    {
        // 创建重力移动向量：只包含垂直方向的移动
        Vector3 gravityMove = new Vector3(0, _velocity.y * Time.deltaTime, 0);

        // 如果有重力移动，应用它
        if (gravityMove != Vector3.zero)
        {
            // 调用CharacterController的Move方法应用重力移动
            _controller.Move(gravityMove);
        }
    }

    // 自由移动函数：没有锁定目标时的移动逻辑
    void Freemove()
    {
        // 如果有输入（玩家按下了移动键）
        if (_move != Vector2.zero)
        {
            // 将2D输入转换为3D方向向量，忽略Y轴（垂直方向）
            Vector3 inputDir = new Vector3(_move.x, 0, _move.y).normalized;

            // 计算目标旋转角度：
            // 1. Atan2计算输入方向相对于世界坐标系的角度（弧度）
            // 2. Rad2Deg将弧度转换为度
            // 3. 加上摄像机的Y轴旋转，使移动方向基于摄像机视角
            _targetRot = Mathf.Atan2(inputDir.x, inputDir.z) * Mathf.Rad2Deg + _mainCamera.transform.eulerAngles.y;

            // 平滑旋转：使角色逐渐转向目标方向，而不是瞬间转向
            float rotation = Mathf.SmoothDampAngle(
                transform.eulerAngles.y,     // 当前旋转角度
                _targetRot,                  // 目标旋转角度
                ref _rotationVelocity,       // 引用旋转速度变量，用于平滑计算
                rotationSmoothTime           // 平滑时间，控制旋转速度
            );

            // 应用旋转到角色
            transform.rotation = Quaternion.Euler(0.0f, rotation, 0.0f);

            // 计算水平移动方向：
            // 将世界坐标系的前方向（Vector3.forward）根据目标旋转角度旋转
            Vector3 moveDirection = Quaternion.Euler(0f, _targetRot, 0f) * Vector3.forward;

            // 创建水平移动向量：
            // 1. 归一化确保方向正确
            // 2. 乘以移动速度
            // 3. 乘以Time.deltaTime确保帧率无关
            Vector3 horizontalMove = moveDirection.normalized * moveSpeed * Time.deltaTime;

            // 应用水平移动：调用CharacterController的Move方法
            _controller.Move(horizontalMove);
        }

        // 更新动画参数：根据移动状态更新动画
        UpdateFreeAnimations();
    }

    // 锁定移动函数：有锁定目标时的移动逻辑
    void Lockmove()
    {
        // 计算到锁定目标的水平方向向量
        Vector3 dir = LockTarget.position - transform.position;
        dir.y = 0; // 忽略Y轴差异，只关心水平方向

        // 即使锁定目标，也允许角色移动
        if (_move != Vector2.zero)
        {
            // 基于摄像机计算移动方向（与自由移动相同）
            Vector3 inputDir = new Vector3(_move.x, 0, _move.y).normalized;
            float targetAngle = Mathf.Atan2(inputDir.x, inputDir.z) * Mathf.Rad2Deg + _mainCamera.transform.eulerAngles.y;
            Vector3 moveDirection = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;

            // 应用水平移动
            Vector3 horizontalMove = moveDirection.normalized * moveSpeed * Time.deltaTime;
            _controller.Move(horizontalMove);
        }

        // 平滑旋转面向目标：使角色逐渐转向锁定目标
        if (dir != Vector3.zero)
        {
            // 计算面向目标的旋转
            Quaternion targetRotation = Quaternion.LookRotation(dir);

            // 使用球面插值平滑旋转
            transform.rotation = Quaternion.Slerp(transform.rotation,targetRotation,Time.deltaTime * 5f);
        }

        // 更新锁定状态下的动画参数
        UpdateLockAnimations();
    }

    // 更新自由动画函数：控制自由移动状态下的动画
    void UpdateFreeAnimations()
    {
        // 确保动画控制器存在
        if (_animator != null)
        {
            // 获取当前的AxisY参数值（通常控制前后移动）
            var AxisY = _animator.GetFloat("AxisY");

            // 平滑过渡到目标值：_move.magnitude是输入向量的长度（0-1）
            AxisY = Mathf.MoveTowards(AxisY, _move.magnitude, Time.deltaTime * 5f);

            // 设置AxisY参数到动画控制器
            _animator.SetFloat("AxisY", AxisY);

            // 获取当前的AxisX参数值（通常控制左右移动）
            var AxisX = _animator.GetFloat("AxisX");

            // 平滑过渡到目标值：_move.x是输入的X分量（-1到1）
            AxisX = Mathf.MoveTowards(AxisX, _move.x, Time.deltaTime * 5f);

            // 设置AxisX参数到动画控制器
            _animator.SetFloat("AxisX", AxisX);
        }
    }

    // 更新锁定动画函数：控制锁定目标状态下的动画
    void UpdateLockAnimations()
    {
        // 确保动画控制器存在    
        if (_animator != null)
        {
            // 获取当前的动画参数值
            var AxisX = _animator.GetFloat("AxisX");
            var AxisY = _animator.GetFloat("AxisY");

            // 根据输入设置动画参数
            AxisX = Mathf.MoveTowards(AxisX, _move.x, Time.deltaTime * 5f);
            AxisY = Mathf.MoveTowards(AxisY, _move.y, Time.deltaTime * 5f);

            // 更新动画参数
            _animator.SetFloat("AxisX", AxisX);
            _animator.SetFloat("AxisY", AxisY);
        }
    }

    // 移动输入变量：存储来自输入系统的移动向量
    Vector2 _move;

    // 移动输入回调函数：当输入系统检测到移动输入时调用
    void OnMove(InputValue value)
    {
        // 从输入系统获取2D向量值（通常来自WASD、摇杆等）
        _move = value.Get<Vector2>();
    }
}