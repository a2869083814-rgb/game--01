using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 第三人称摄像机控制器
/// 功能：控制摄像机围绕目标旋转、缩放，并处理与环境的碰撞检测
/// </summary>
public class ThirdPersonCameraController : MonoBehaviour
{
    #region 摄像机目标设置

    [Header("摄像机目标")]
    [Tooltip("摄像机跟随的目标对象")]
    public Transform CameraTarget;

    #endregion

    #region 缩放设置

    [Header("缩放设置")]
    [Tooltip("缩放速度")]
    public float ZoomSpeed = 5f;
    [Tooltip("摄像机与目标的最小距离")]
    public float MinZoomDistance = 2f;
    [Tooltip("摄像机与目标的最大距离")]
    public float MaxZoomDistance = 10f;
    [Tooltip("当前摄像机与目标的距离")]
    public float CurrentZoomDistance = 5f;

    #endregion

    #region 碰撞检测设置

    [Header("碰撞检测设置")]
    [Tooltip("检测哪些图层的碰撞")]
    public LayerMask CollisionLayers = ~0;
    [Tooltip("检测碰撞时使用的球体半径")]
    public float CollisionRadius = 0.3f;
    [Tooltip("从目标位置开始检测的偏移距离")]
    public float CollisionOffset = 0.5f;
    [Tooltip("碰撞后调整摄像机位置的平滑速度")]
    public float CollisionAdjustSpeed = 10f;
    [Tooltip("是否启用碰撞检测功能")]
    public bool EnableCollisionDetection = true;
    [Tooltip("同时使用射线和球体两种检测方式，提高准确性")]
    public bool DualDetectionMode = true;

    #endregion

    #region 旋转角度限制

    [Header("旋转角度限制")]
    [Tooltip("摄像机向上旋转的最大角度")]
    public float TopClamp = 70f;
    [Tooltip("摄像机向下旋转的最大角度")]
    public float BottomClamp = 30f;

    #endregion

    #region 私有变量声明

    // 摄像机相关
    private Camera _mainCamera;
    private float _targetYaw;      // 水平旋转角度
    private float _targetPitch;    // 垂直旋转角度

    // 输入相关
    private Vector2 _lookInput;    // 视角输入值
    private PlayerInput _playerInput;

    // 缩放相关
    private float _zoomAccumulator = 0f;

    // 常量定义
    private const float INPUT_THRESHOLD = 0.01f;           // 输入阈值，小于此值忽略
    private const float ZOOM_INPUT_MULTIPLIER = 0.1f;     // 缩放输入乘数
    private const float CAMERA_MOVE_SMOOTH_FACTOR = 5f;   // 摄像机移动平滑系数

    #endregion

    #region Unity生命周期方法

    /// <summary>
    /// 游戏开始时执行一次，用于初始化
    /// </summary>
    private void Start()
    {
        InitializeCamera();
        InitializeRotation();
        EnsurePlayerInputComponent();
    }

    /// <summary>
    /// 每帧执行一次，处理输入和更新摄像机
    /// </summary>
    private void Update()
    {
        // 安全检查：确保目标存在
        if (CameraTarget == null)
        {
            return;
        }

        // 处理鼠标滚轮缩放输入
        HandleZoomInput();

        // 处理摄像机旋转
        HandleRotation();

        // 更新摄像机位置
        UpdateCameraPosition();
    }

    #endregion

    #region 初始化方法

    /// <summary>
    /// 初始化摄像机，查找主摄像机并设置初始状态
    /// </summary>
    private void InitializeCamera()
    {
        // 查找场景中的主摄像机
        _mainCamera = Camera.main;

        // 如果通过Camera.main没找到，尝试通过标签查找
        if (_mainCamera == null)
        {
            GameObject mainCameraObj = GameObject.FindGameObjectWithTag("MainCamera");
            if (mainCameraObj != null)
            {
                _mainCamera = mainCameraObj.GetComponent<Camera>();
            }
        }

        // 如果还是没找到摄像机，禁用脚本并报错
        if (_mainCamera == null)
        {
            Debug.LogError("未找到主摄像机！请确保场景中有标签为MainCamera的摄像机");
            enabled = false;
            return;
        }

        // 如果目标存在，初始化摄像机距离
        if (CameraTarget != null)
        {
            _targetYaw = CameraTarget.rotation.eulerAngles.y;
            CurrentZoomDistance = Vector3.Distance(
                _mainCamera.transform.position,
                CameraTarget.position
            );
        }
    }

    /// <summary>
    /// 初始化旋转角度，从目标对象的当前旋转中读取
    /// </summary>
    private void InitializeRotation()
    {
        if (CameraTarget != null)
        {
            Vector3 targetRotation = CameraTarget.rotation.eulerAngles;
            _targetYaw = targetRotation.y;
            _targetPitch = targetRotation.x;
        }
    }

    /// <summary>
    /// 确保对象上有PlayerInput组件，用于处理输入系统
    /// </summary>
    private void EnsurePlayerInputComponent()
    {
        _playerInput = GetComponent<PlayerInput>();
        if (_playerInput == null)
        {
            _playerInput = gameObject.AddComponent<PlayerInput>();
            Debug.LogWarning("自动添加了PlayerInput组件，请配置Input Actions Asset");
        }
    }

    #endregion

    #region 输入处理方法

    /// <summary>
    /// 处理鼠标滚轮缩放输入
    /// </summary>
    private void HandleZoomInput()
    {
        // 读取鼠标滚轮输入值
        float scrollInput = Input.mouseScrollDelta.y;

        // 如果有有效的滚轮输入，更新缩放距离
        if (Mathf.Abs(scrollInput) > INPUT_THRESHOLD)
        {
            // 计算缩放变化量
            float zoomChange = scrollInput * ZoomSpeed * Time.deltaTime * 10f;

            // 更新当前距离并限制在最小最大值之间
            CurrentZoomDistance -= zoomChange;
            CurrentZoomDistance = Mathf.Clamp(CurrentZoomDistance, MinZoomDistance, MaxZoomDistance);
        }
    }

    /// <summary>
    /// 处理摄像机旋转逻辑
    /// </summary>
    private void HandleRotation()
    {
        // 安全检查：确保目标存在
        if (CameraTarget == null)
            return;

        // 如果有视角输入，更新旋转角度
        if (_lookInput.sqrMagnitude >= INPUT_THRESHOLD)
        {
            _targetYaw += _lookInput.x;      // 水平旋转
            _targetPitch -= _lookInput.y;    // 垂直旋转
        }

        // 限制垂直旋转角度在设定的范围内
        _targetPitch = ClampAngle(_targetPitch, -BottomClamp, TopClamp);

        // 将新的旋转角度应用到目标对象
        CameraTarget.rotation = Quaternion.Euler(
            _targetPitch,
            _targetYaw,
            0f
        );
    }

    #endregion

    #region 摄像机位置更新方法

    /// <summary>
    /// 更新摄像机的位置，处理碰撞检测和平滑移动
    /// </summary>
    private void UpdateCameraPosition()
    {
        // 安全检查：确保目标存在且摄像机有效
        if (CameraTarget == null || _mainCamera == null)
            return;

        // 步骤1：计算不考虑碰撞的理想位置
        Vector3 idealPosition = CalculateIdealCameraPosition();

        // 步骤2：根据碰撞检测计算实际位置
        Vector3 finalPosition = EnableCollisionDetection
            ? CalculateCollisionAdjustedPosition(idealPosition)
            : idealPosition;

        // 步骤3：平滑移动摄像机到最终位置
        _mainCamera.transform.position = Vector3.Lerp(
            _mainCamera.transform.position,
            finalPosition,
            Time.deltaTime * ZoomSpeed * CAMERA_MOVE_SMOOTH_FACTOR
        );

        // 步骤4：确保摄像机始终看向目标
        _mainCamera.transform.LookAt(CameraTarget.position);
    }

    /// <summary>
    /// 计算不考虑碰撞的理想摄像机位置
    /// 原理：从目标位置向后移动一定距离
    /// </summary>
    private Vector3 CalculateIdealCameraPosition()
    {
        // 目标位置 - 目标前方方向 × 当前距离
        return CameraTarget.position - CameraTarget.forward * CurrentZoomDistance;
    }

    /// <summary>
    /// 计算考虑碰撞的实际摄像机位置
    /// 使用双检测模式提高准确性
    /// </summary>
    /// <param name="idealPosition">理想位置</param>
    /// <returns>考虑碰撞后的安全位置</returns>
    private Vector3 CalculateCollisionAdjustedPosition(Vector3 idealPosition)
    {
        // 计算摄像机方向向量
        Vector3 cameraDirection = (idealPosition - CameraTarget.position).normalized;
        float desiredDistance = CurrentZoomDistance;

        // 设置检测起点和最大检测距离
        Vector3 detectionStart = CameraTarget.position + cameraDirection * CollisionOffset;
        float maxDetectionDistance = desiredDistance - CollisionOffset;

        // 变量存储检测结果
        bool hitDetected = false;
        float closestHitDistance = maxDetectionDistance;
        RaycastHit closestHit = new RaycastHit();

        // 方法1：使用射线检测（对所有碰撞体有效）
        RaycastHit raycastHit;
        bool raycastResult = Physics.Raycast(
            detectionStart,
            cameraDirection,
            out raycastHit,
            maxDetectionDistance,
            CollisionLayers,
            QueryTriggerInteraction.Ignore
        );

        // 更新最近碰撞信息
        if (raycastResult && raycastHit.distance < closestHitDistance)
        {
            closestHitDistance = raycastHit.distance;
            closestHit = raycastHit;
            hitDetected = true;
        }

        // 方法2：使用球体检测（对凸面Mesh Collider更准确）
        if (DualDetectionMode)
        {
            RaycastHit sphereHit;
            bool spherecastResult = Physics.SphereCast(
                detectionStart,
                CollisionRadius,
                cameraDirection,
                out sphereHit,
                maxDetectionDistance,
                CollisionLayers,
                QueryTriggerInteraction.Ignore
            );

            // 更新最近碰撞信息
            if (spherecastResult && sphereHit.distance < closestHitDistance)
            {
                closestHitDistance = sphereHit.distance;
                closestHit = sphereHit;
                hitDetected = true;
            }
        }

        // 如果有碰撞，计算安全位置
        if (hitDetected)
        {
            // 根据碰撞体类型获取安全边距
            float safetyMargin = GetCollisionSafetyMargin(closestHit.collider);

            // 计算安全距离（确保不会离目标太近）
            float safeDistance = Mathf.Max(closestHitDistance - safetyMargin, MinZoomDistance * 0.5f);

            // 确保调整后的距离不超过原始距离
            float adjustedDistance = Mathf.Min(safeDistance, desiredDistance);

            // 返回安全位置
            return CameraTarget.position + cameraDirection * (adjustedDistance + CollisionOffset);
        }

        // 如果没有碰撞，返回理想位置
        return idealPosition;
    }

    /// <summary>
    /// 根据碰撞体类型获取不同的安全边距
    /// 原理：不同类型碰撞体需要不同的安全距离
    /// </summary>
    private float GetCollisionSafetyMargin(Collider collider)
    {
        if (collider == null)
            return CollisionRadius;

        // 根据碰撞体类型返回不同的安全边距
        if (collider is MeshCollider)
        {
            // Mesh Collider通常更复杂，需要更大安全距离
            return CollisionRadius * 1.5f;
        }
        else if (collider is BoxCollider || collider is SphereCollider || collider is CapsuleCollider)
        {
            // 基础碰撞体使用标准安全距离
            return CollisionRadius;
        }
        else
        {
            // 其他类型使用更大的安全距离
            return CollisionRadius * 2f;
        }
    }

    #endregion

    #region 工具方法

    /// <summary>
    /// 将角度限制在指定范围内
    /// 原理：处理角度超过360度或小于-360度的情况
    /// </summary>
    private static float ClampAngle(float angle, float minAngle, float maxAngle)
    {
        // 处理角度超出360度的情况
        if (angle < -360f) angle += 360f;
        if (angle > 360f) angle -= 360f;

        // 限制在最小最大范围内
        return Mathf.Clamp(angle, minAngle, maxAngle);
    }

    #endregion

    #region 输入系统回调方法

    /// <summary>
    /// 输入系统：视角控制回调
    /// 当玩家移动鼠标或摇杆时触发
    /// </summary>
    public void OnLook(InputValue inputValue)
    {
        _lookInput = inputValue.Get<Vector2>();
    }

    /// <summary>
    /// 输入系统：鼠标缩放回调
    /// 当玩家滚动鼠标滚轮时触发
    /// </summary>
    public void OnMouseZoom(InputValue inputValue)
    {
        Vector2 scrollDelta = inputValue.Get<Vector2>();
        if (Mathf.Abs(scrollDelta.y) >= INPUT_THRESHOLD)
        {
            _zoomAccumulator += scrollDelta.y;
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
        if (CameraTarget == null)
            return;

        // 只在游戏运行时绘制碰撞检测信息
        if (Application.isPlaying && EnableCollisionDetection)
        {
            DrawCollisionDetectionGizmos();
        }

        // 绘制缩放范围信息
        DrawZoomRangeGizmos();
    }

    /// <summary>
    /// 绘制碰撞检测相关的调试信息
    /// </summary>
    private void DrawCollisionDetectionGizmos()
    {
        // 计算检测方向
        Vector3 cameraDirection = (_mainCamera.transform.position - CameraTarget.position).normalized;
        Vector3 rayOrigin = CameraTarget.position + cameraDirection * CollisionOffset;
        float maxDistance = CurrentZoomDistance - CollisionOffset;

        // 绘制射线检测范围（红色）
        Gizmos.color = Color.red;
        Gizmos.DrawLine(rayOrigin, rayOrigin + cameraDirection * maxDistance);

        // 绘制球体检测范围（青色）
        Gizmos.color = Color.cyan;
        for (float i = 0; i < maxDistance; i += maxDistance / 10f)
        {
            Gizmos.DrawWireSphere(rayOrigin + cameraDirection * i, CollisionRadius);
        }

        // 绘制理想位置（绿色）
        Gizmos.color = Color.green;
        Vector3 idealPosition = CalculateIdealCameraPosition();
        Gizmos.DrawWireSphere(idealPosition, 0.2f);

        // 绘制实际位置（洋红色）
        Vector3 actualPosition = CalculateCollisionAdjustedPosition(idealPosition);
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(actualPosition, 0.2f);
        Gizmos.DrawLine(idealPosition, actualPosition);
    }

    /// <summary>
    /// 绘制缩放范围相关的调试信息
    /// </summary>
    private void DrawZoomRangeGizmos()
    {
        // 红色：最小缩放距离
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(CameraTarget.position, MinZoomDistance);

        // 绿色：最大缩放距离
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(CameraTarget.position, MaxZoomDistance);

        // 黄色：当前缩放距离
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(CameraTarget.position, CurrentZoomDistance);
    }

    #endregion
}