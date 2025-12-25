using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonCameraController : MonoBehaviour
{
    [Header("Cinemachine")]
    [Tooltip("跟随的目标")]
    public Transform CameraTarget;

    [Header("Zoom Settings")]
    [Tooltip("缩放速度")]
    public float ZoomSpeed = 5f;
    [Tooltip("最小缩放距离")]
    public float MinZoomDistance = 2f;
    [Tooltip("最大缩放距离")]
    public float MaxZoomDistance = 10f;
    [Header("当前缩放距离")]
    public float CurrentZoomDistance = 5f;

    [Header("碰撞检测设置")]
    [Tooltip("检测碰撞的图层")]
    public LayerMask CollisionLayers = ~0;
    [Tooltip("检测球体半径")]
    public float CollisionRadius = 0.3f;
    [Tooltip("检测起始偏移")]
    public float CollisionOffset = 0.5f;
    [Tooltip("碰撞后调整距离的平滑速度")]
    public float CollisionAdjustSpeed = 10f;
    [Tooltip("是否启用碰撞检测")]
    public bool EnableCollisionDetection = true;
    [Tooltip("双模式检测：同时使用射线和球体检测")]
    public bool DualDetectionMode = true;

    [Tooltip("上移动的最大角度")]
    public float TopClamp = 70f;
    [Tooltip("下移动的最大角度")]
    public float BottomClamp = 30f;

    // 私有字段
    private Camera _mainCamera;
    private float _cinemachineTargetYaw;
    private float _cinemachineTargetPitch;
    private float _zoomAccumulator = 0f;
    private Vector2 _look;
    private PlayerInput _playerInput;

    // 常量
    private const float _threshold = 0.01f;
    private const float _zoomInputMultiplier = 0.1f;
    private const float _cameraMoveSmoothFactor = 5f;

    private void Start()
    {
        InitializeCamera();
        InitializeRotation();
        EnsurePlayerInputComponent();
    }

    private void Update()
    {
        // 检查CameraTarget是否有效
        if (CameraTarget == null || System.Object.ReferenceEquals(CameraTarget, null))
        {
            return;
        }

        // 直接读取鼠标滚轮输入
        float scrollInput = Input.mouseScrollDelta.y;

        // 如果有滚轮输入，更新缩放距离
        if (Mathf.Abs(scrollInput) > _threshold)
        {
            float zoomChange = scrollInput * ZoomSpeed * Time.deltaTime * 10f;
            CurrentZoomDistance -= zoomChange;
            CurrentZoomDistance = Mathf.Clamp(CurrentZoomDistance, MinZoomDistance, MaxZoomDistance);
        }

        HandleRotation();
        UpdateCameraZoom();
    }

    private void InitializeCamera()
    {
        _mainCamera = Camera.main;
        if (_mainCamera == null)
        {
            GameObject mainCameraObj = GameObject.FindGameObjectWithTag("MainCamera");
            if (mainCameraObj != null)
            {
                _mainCamera = mainCameraObj.GetComponent<Camera>();
            }
        }

        if (_mainCamera == null)
        {
            Debug.LogError("未找到主摄像机！请确保场景中有标签为MainCamera的摄像机");
            enabled = false;
            return;
        }

        if (CameraTarget != null)
        {
            _cinemachineTargetYaw = CameraTarget.rotation.eulerAngles.y;
            CurrentZoomDistance = Vector3.Distance(
                _mainCamera.transform.position,
                CameraTarget.position
            );
        }
    }

    private void InitializeRotation()
    {
        if (CameraTarget != null)
        {
            Vector3 euler = CameraTarget.rotation.eulerAngles;
            _cinemachineTargetYaw = euler.y;
            _cinemachineTargetPitch = euler.x;
        }
    }

    private void EnsurePlayerInputComponent()
    {
        _playerInput = GetComponent<PlayerInput>();
        if (_playerInput == null)
        {
            _playerInput = gameObject.AddComponent<PlayerInput>();
            Debug.LogWarning("自动添加了PlayerInput组件，请配置Input Actions Asset");
        }
    }

    private void HandleRotation()
    {
        if (CameraTarget == null || System.Object.ReferenceEquals(CameraTarget, null))
            return;

        if (_look.sqrMagnitude >= _threshold)
        {
            _cinemachineTargetYaw += _look.x;
            _cinemachineTargetPitch -= _look.y;
        }

        _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, -BottomClamp, TopClamp);

        CameraTarget.rotation = Quaternion.Euler(
            _cinemachineTargetPitch,
            _cinemachineTargetYaw,
            0f
        );
    }

    private void UpdateCameraZoom()
    {
        if (CameraTarget == null || System.Object.ReferenceEquals(CameraTarget, null) || _mainCamera == null)
            return;

        // 计算理想相机位置（不考虑碰撞）
        Vector3 idealPosition = CalculateIdealCameraPosition();

        // 计算实际相机位置（考虑碰撞）
        Vector3 finalPosition = EnableCollisionDetection
            ? CalculateCollisionAdjustedPosition(idealPosition)
            : idealPosition;

        // 平滑移动相机
        _mainCamera.transform.position = Vector3.Lerp(
            _mainCamera.transform.position,
            finalPosition,
            Time.deltaTime * ZoomSpeed * _cameraMoveSmoothFactor
        );

        // 始终看向目标
        _mainCamera.transform.LookAt(CameraTarget.position);
    }

    /// <summary>
    /// 计算理想的相机位置（不考虑碰撞）
    /// </summary>
    private Vector3 CalculateIdealCameraPosition()
    {
        return CameraTarget.position - CameraTarget.forward * CurrentZoomDistance;
    }

    /// <summary>
    /// 计算考虑碰撞的相机位置
    /// 双模式检测：同时处理Box Collider和Mesh Collider
    /// </summary>
    private Vector3 CalculateCollisionAdjustedPosition(Vector3 idealPosition)
    {
        // 计算从目标到理想位置的向量
        Vector3 cameraDirection = (idealPosition - CameraTarget.position).normalized;
        float desiredDistance = CurrentZoomDistance;

        // 从目标位置向相机方向发射射线
        Vector3 rayOrigin = CameraTarget.position + cameraDirection * CollisionOffset;
        float maxDistance = desiredDistance - CollisionOffset;

        // 方法1：首先使用简单的Raycast检测（对所有碰撞体都有效）
        RaycastHit rayHit;
        bool raycastHit = Physics.Raycast(
            rayOrigin,
            cameraDirection,
            out rayHit,
            maxDistance,
            CollisionLayers,
            QueryTriggerInteraction.Ignore
        );

        // 方法2：使用SphereCast检测（对凸面Mesh Collider有效）
        RaycastHit sphereHit;
        bool spherecastHit = Physics.SphereCast(
            rayOrigin,
            CollisionRadius,
            cameraDirection,
            out sphereHit,
            maxDistance,
            CollisionLayers,
            QueryTriggerInteraction.Ignore
        );

        // 选择最近的有效碰撞点
        float closestHitDistance = maxDistance;
        RaycastHit closestHit = new RaycastHit();
        bool hasValidHit = false;

        // 检查Raycast结果
        if (raycastHit && rayHit.distance < closestHitDistance && rayHit.collider != null)
        {
            closestHitDistance = rayHit.distance;
            closestHit = rayHit;
            hasValidHit = true;
        }

        // 检查SphereCast结果
        if (spherecastHit && sphereHit.distance < closestHitDistance && sphereHit.collider != null)
        {
            closestHitDistance = sphereHit.distance;
            closestHit = sphereHit;
            hasValidHit = true;
        }

        // 如果有碰撞，计算调整后的位置
        if (hasValidHit)
        {
            // 根据碰撞体类型调整安全距离
            float safetyMargin = GetSafetyMargin(closestHit.collider);
            float safeDistance = Mathf.Max(closestHitDistance - safetyMargin, MinZoomDistance * 0.5f);

            // 调整距离，但不要超过当前的缩放距离
            float adjustedDistance = Mathf.Min(safeDistance, desiredDistance);

            // 返回调整后的位置
            return CameraTarget.position + cameraDirection * (adjustedDistance + CollisionOffset);
        }

        // 如果没有碰撞，返回理想位置
        return idealPosition;
    }

    /// <summary>
    /// 根据碰撞体类型返回不同的安全边距
    /// </summary>
    private float GetSafetyMargin(Collider collider)
    {
        if (collider == null)
            return CollisionRadius;

        // 根据碰撞体类型调整安全边距
        if (collider is MeshCollider meshCollider)
        {
            // Mesh Collider可能需要更大的安全边距
            return CollisionRadius * 1.5f;
        }
        else if (collider is BoxCollider || collider is SphereCollider || collider is CapsuleCollider)
        {
            // 基本碰撞体使用标准边距
            return CollisionRadius;
        }
        else
        {
            // 其他类型使用较大的安全边距
            return CollisionRadius * 2f;
        }
    }

    private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
    {
        if (lfAngle < -360f) lfAngle += 360f;
        if (lfAngle > 360f) lfAngle -= 360f;
        return Mathf.Clamp(lfAngle, lfMin, lfMax);
    }

    // 输入系统回调
    public void OnLook(InputValue Value)
    {
        _look = Value.Get<Vector2>();
    }

    public void OnMouseZoom(InputValue Value)
    {
        Vector2 ScrollDelta = Value.Get<Vector2>();
        if (Mathf.Abs(ScrollDelta.y) >= _threshold)
        {
            _zoomAccumulator += ScrollDelta.y;
        }
    }

    #region 调试辅助

    /// <summary>
    /// 在编辑器中绘制调试信息
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (CameraTarget == null)
            return;

        // 绘制碰撞检测范围
        if (Application.isPlaying && EnableCollisionDetection)
        {
            // 绘制检测射线
            Vector3 cameraDirection = (_mainCamera.transform.position - CameraTarget.position).normalized;
            Vector3 rayOrigin = CameraTarget.position + cameraDirection * CollisionOffset;
            float maxDistance = CurrentZoomDistance - CollisionOffset;

            // 射线检测范围
            Gizmos.color = Color.red;
            Gizmos.DrawLine(rayOrigin, rayOrigin + cameraDirection * maxDistance);

            // 球体检测范围
            Gizmos.color = Color.cyan;
            for (float i = 0; i < maxDistance; i += maxDistance / 10f)
            {
                Gizmos.DrawWireSphere(rayOrigin + cameraDirection * i, CollisionRadius);
            }

            // 绘制理想位置
            Gizmos.color = Color.green;
            Vector3 idealPosition = CalculateIdealCameraPosition();
            Gizmos.DrawWireSphere(idealPosition, 0.2f);

            // 绘制实际位置
            Vector3 actualPosition = CalculateCollisionAdjustedPosition(idealPosition);
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(actualPosition, 0.2f);
            Gizmos.DrawLine(idealPosition, actualPosition);
        }

        // 绘制缩放范围
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(CameraTarget.position, MinZoomDistance);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(CameraTarget.position, MaxZoomDistance);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(CameraTarget.position, CurrentZoomDistance);
    }

    #endregion
}