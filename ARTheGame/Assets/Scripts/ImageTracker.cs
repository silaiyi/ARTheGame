using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.UI;

/// <summary>
/// AR图像追踪管理器，负责处理棋盘生成、交互逻辑和棋子追踪
/// 主要功能：
/// 1. 通过图像追踪生成棋盘
/// 2. 支持手势旋转/缩放棋盘
/// 3. 自动重新定位棋盘到视野中心
/// 4. 追踪O/X标记生成对应棋子
/// 5. 提供棋盘重置功能
/// </summary>
public class ImageTracker : MonoBehaviour
{
    // ==================== AR 设置 ====================
    [Header("AR Settings")]
    [Tooltip("棋盘预制体")]
    public GameObject chessboardPrefab;
    [Tooltip("默认生成距离（米）")]
    public float defaultDistance = 1.0f;
    [Tooltip("旋转速度系数")]
    public float rotationSpeed = 20f;
    [Tooltip("最小缩放比例")]
    public float minScale = 0.1f;
    [Tooltip("最大缩放比例")]
    public float maxScale = 2.0f;
    [Tooltip("垂直方向偏移量")]
    public float verticalOffset = 0f;
    [Tooltip("水平方向偏移量")]
    public float horizontalOffset = 0f;

    // ==================== UI 元素 ====================
    [Header("UI Elements")]
    [Tooltip("确认放置的UI面板")]
    public GameObject confirmationUI;
    [Tooltip("确认按钮的RectTransform")]
    public RectTransform confirmationButton;

    // ==================== 重新定位设置 ====================
    [Header("Reposition Settings")]
    [Tooltip("重新定位按钮")]
    public Button repositionButton;
    [Tooltip("移动动画时长（秒）")]
    public float moveDuration = 0.5f;
    [Tooltip("屏幕边缘留白比例")]
    public float viewportMargin = 0.1f;

    // ==================== 交互设置 ====================
    [Header("Interaction")]
    [Tooltip("缩放灵敏度")]
    public float scaleSensitivity = 0.01f;

    // ==================== 视口设置 ====================
    [Header("Viewport Settings")]
    [Tooltip("垂直方向的位置偏移（0-1）")]
    public float verticalPositionFactor = 0.25f;
    [Tooltip("距离摄像头的基准偏移（米）")]
    public float cameraDistanceOffset = 0.8f;
    [Tooltip("地面检测射线长度（米）")]
    public float groundCheckDistance = 2f;

    // ==================== 旋转设置 ====================
    [Header("Rotation Settings")]
    [Tooltip("最大垂直旋转角度")]
    public float maxVerticalAngle = 90f;

    // ==================== 生成设置 ====================
    [Header("Spawn Settings")]
    [Tooltip("初始生成旋转偏移")]
    public Vector3 spawnRotationOffset = Vector3.zero;

    // ==================== 棋子预制体 ====================
    [Header("Piece Prefabs")]
    [Tooltip("O棋子预制体")]
    public GameObject OPiecePrefab;
    [Tooltip("X棋子预制体")]
    public GameObject XPiecePrefab;

    // 私有变量
    private ARTrackedImageManager trackedImageManager; // AR图像追踪管理器
    private GameObject spawnedBoard; // 已生成的棋盘实例
    private Camera arCamera; // AR摄像机
    private Vector2 lastTouchPosition; // 最后触摸位置
    private float initialDistance; // 初始触摸距离（用于缩放）
    private ARTrackedImage currentTrackedImage; // 当前追踪的图像
    private Vector3 targetPosition; // 目标位置（用于平滑移动）
    private Quaternion targetRotation; // 目标旋转（用于平滑移动）
    private bool isMoving = false; // 是否正在移动中
    private Vector2 touchStartPos; // 触摸起始位置
    private float currentRotationY; // 当前Y轴旋转值
    private float currentRotationX; // 当前X轴旋转值
    private GameObject currentBoard; // 当前操作的棋盘

    // 初始化组件
    void Awake()
    {
        // 获取AR组件引用
        trackedImageManager = GetComponent<ARTrackedImageManager>();
        arCamera = GetComponentInChildren<Camera>();

        // 初始化重新定位按钮
        if (repositionButton != null)
        {
            repositionButton.gameObject.SetActive(false);
            repositionButton.onClick.RemoveAllListeners();
            repositionButton.onClick.AddListener(MoveBoardToView);
        }
    }

    /// <summary>
    /// 每帧更新处理触摸输入
    /// </summary>
    void Update()
    {
        if (!spawnedBoard || isMoving) return;
        if (Input.touchCount == 0) touchStartPos = Vector2.zero;
        HandleTouchInput();
    }

    // ==================== AR图像追踪事件处理 ====================
    void OnEnable() => trackedImageManager.trackedImagesChanged += OnTrackedImagesChanged;
    void OnDisable() => trackedImageManager.trackedImagesChanged -= OnTrackedImagesChanged;

    /// <summary>
    /// 当追踪图像发生变化时调用
    /// </summary>
    void OnTrackedImagesChanged(ARTrackedImagesChangedEventArgs eventArgs)
    {
        foreach (var trackedImage in eventArgs.added) UpdateTrackedImage(trackedImage);
        foreach (var trackedImage in eventArgs.updated) UpdateTrackedImage(trackedImage);
    }

    /// <summary>
    /// 更新追踪图像状态
    /// </summary>
    void UpdateTrackedImage(ARTrackedImage trackedImage)
    {
        // 处理棋子追踪（O/X标记）
        if ((trackedImage.referenceImage.name == "O" || trackedImage.referenceImage.name == "X") &&
            trackedImage.trackingState == TrackingState.Tracking)
        {
            HandlePieceTracking(trackedImage);
        }
        // 处理棋盘定位（Card标记）
        else if (trackedImage.referenceImage.name == "Card")
        {
            if (!BoardExists && trackedImage.trackingState == TrackingState.Tracking)
            {
                currentTrackedImage = trackedImage;
                ShowConfirmationUI(trackedImage.transform.position);
            }
            else if (BoardExists) confirmationUI.SetActive(false);
        }
    }

    // ==================== UI控制 ====================
    /// <summary>
    /// 显示确认放置UI
    /// </summary>
    void ShowConfirmationUI(Vector3 worldPosition)
    {
        Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPosition);
        confirmationButton.position = screenPos;
        confirmationUI.SetActive(true);
    }

    /// <summary>
    /// 确认放置按钮点击事件
    /// </summary>
    public void ConfirmPlacement()
    {
        if (arCamera == null)
        {
            Debug.LogError("AR Camera not found!");
            return;
        }

        // 计算生成位置（摄像机前方+偏移）
        Vector3 spawnPosition = arCamera.transform.position
            + arCamera.transform.forward * defaultDistance
            + arCamera.transform.up * verticalOffset
            + arCamera.transform.right * horizontalOffset;

        // 计算初始旋转（面向摄像机）
        Quaternion spawnRotation = Quaternion.LookRotation(
            arCamera.transform.forward,
            Vector3.up
        ) * Quaternion.Euler(spawnRotationOffset);

        // 显示重新定位按钮
        if (repositionButton != null)
        {
            repositionButton.gameObject.SetActive(true);
            RepositionButtonToCorner();
        }

        // 生成新棋盘
        if (spawnedBoard != null) Destroy(spawnedBoard);
        spawnedBoard = Instantiate(chessboardPrefab, spawnPosition, spawnRotation);
        confirmationUI.SetActive(false);
        currentBoard = spawnedBoard;

        // 初始化旋转值
        currentRotationX = 0f;
        currentRotationY = spawnRotation.eulerAngles.y;
        spawnedBoard.transform.rotation = Quaternion.Euler(currentRotationX, currentRotationY, 0);

        // 重置追踪管理器
        if (trackedImageManager != null)
        {
            trackedImageManager.enabled = false;
            trackedImageManager.enabled = true;
        }
        BoardExists = true;

        // 添加AR锚点
        spawnedBoard.AddComponent<ARAnchor>();
    }

    // ==================== 手势交互 ====================
    /// <summary>
    /// 处理触摸输入（单指旋转/双指缩放）
    /// </summary>
    void HandleTouchInput()
    {
        if (Input.touchCount == 1) HandleRotation();
        if (Input.touchCount == 2) HandleZoom();
    }

    /// <summary>
    /// 处理旋转逻辑
    /// </summary>
    void HandleRotation()
    {
        if (currentBoard == null) return;

        Touch touch = Input.GetTouch(0);
        switch (touch.phase)
        {
            case TouchPhase.Began:
                touchStartPos = touch.position;
                break;

            case TouchPhase.Moved:
                Vector2 delta = touch.deltaPosition * rotationSpeed * Time.deltaTime;
                currentRotationY -= delta.x; // Y轴旋转
                currentRotationX = Mathf.Clamp( // X轴旋转（带限制）
                    currentRotationX + delta.y,
                    -maxVerticalAngle,
                    maxVerticalAngle
                );
                currentBoard.transform.rotation = Quaternion.Euler(currentRotationX, currentRotationY, 0);
                break;
        }
    }

    /// <summary>
    /// 处理缩放逻辑
    /// </summary>
    void HandleZoom()
    {
        if (currentBoard == null) return;

        Touch touch0 = Input.GetTouch(0);
        Touch touch1 = Input.GetTouch(1);

        // 计算两指距离差
        float prevDelta = Vector2.Distance(touch0.position - touch0.deltaPosition, touch1.position - touch1.deltaPosition);
        float currentDelta = Vector2.Distance(touch0.position, touch1.position);
        float scaleDelta = (currentDelta - prevDelta) * scaleSensitivity;

        // 应用缩放限制
        Vector3 newScale = currentBoard.transform.localScale + (Vector3.one * scaleDelta);
        newScale = Vector3.Max(Vector3.one * minScale, Vector3.Min(Vector3.one * maxScale, newScale));
        currentBoard.transform.localScale = newScale;
    }

    // ==================== 重新定位逻辑 ====================
    /// <summary>
    /// 将棋盘移动到摄像机视野中心
    /// </summary>
    void MoveBoardToView() => StartCoroutine(SmoothMoveToCameraView());

    /// <summary>
    /// 平滑移动协程
    /// </summary>
    IEnumerator SmoothMoveToCameraView()
    {
        isMoving = true;
        CalculateOptimalPosition(out targetPosition, out targetRotation);
        Vector3 initialScale = spawnedBoard.transform.localScale;

        float elapsed = 0;
        Vector3 startPos = spawnedBoard.transform.position;
        Quaternion startRot = spawnedBoard.transform.rotation;

        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0, 1, elapsed / moveDuration);

            spawnedBoard.transform.position = Vector3.Lerp(startPos, targetPosition, t);
            spawnedBoard.transform.rotation = Quaternion.Slerp(startRot, targetRotation, t);
            spawnedBoard.transform.localScale = Vector3.Lerp(initialScale, Vector3.one, t);

            // 地面检测
            if (Physics.Raycast(spawnedBoard.transform.position + Vector3.up, Vector3.down, out var finalHit, 2f))
                spawnedBoard.transform.position = finalHit.point + Vector3.up * 0.05f;

            yield return new WaitForEndOfFrame();
        }

        // 最终修正
        spawnedBoard.transform.position = targetPosition;
        spawnedBoard.transform.rotation = targetRotation;
        spawnedBoard.transform.localScale = Vector3.one;
        isMoving = false;
    }

    /// <summary>
    /// 计算最佳位置和旋转（考虑视角和地面检测）
    /// </summary>
    void CalculateOptimalPosition(out Vector3 position, out Quaternion rotation)
    {
        // 计算视口点（屏幕中心偏下）
        Vector3 viewportPoint = new Vector3(
            0.5f,
            verticalPositionFactor,
            arCamera.nearClipPlane + cameraDistanceOffset
        );

        position = arCamera.ViewportToWorldPoint(viewportPoint);
        rotation = Quaternion.LookRotation(arCamera.transform.forward, Vector3.up);

        // 地面检测
        RaycastHit hit;
        Vector3 rayStart = position + Vector3.up * 1f;
        if (Physics.Raycast(rayStart, Vector3.down, out hit, groundCheckDistance))
            position.y = hit.point.y + 0.05f;
        else
            position.y = arCamera.transform.position.y - 0.3f;
    }

    // ==================== 辅助功能 ====================
    /// <summary>
    /// 重新定位按钮到左上角
    /// </summary>
    void RepositionButtonToCorner()
    {
        if (repositionButton == null) return;
        RectTransform rectTransform = repositionButton.GetComponent<RectTransform>();
        if (rectTransform == null) return;

        rectTransform.anchorMin = new Vector2(0, 1);
        rectTransform.anchorMax = new Vector2(0, 1);
        rectTransform.anchoredPosition = new Vector2(200, -100);
    }

    /// <summary>
    /// 处理棋子生成逻辑
    /// </summary>
    void HandlePieceTracking(ARTrackedImage trackedImage)
    {
        if (trackedImage.trackingState != TrackingState.Tracking) return;
        if (trackedImage.GetComponentInChildren<PieceController>() != null) return;

        // 选择对应预制体
        GameObject prefab = trackedImage.referenceImage.name == "O" ? OPiecePrefab : XPiecePrefab;
        if (prefab == null)
        {
            Debug.LogError("棋子Prefab未赋值！");
            return;
        }

        // 实例化棋子
        GameObject piece = Instantiate(
            prefab,
            trackedImage.transform.position + Vector3.up * 0.1f,
            trackedImage.transform.rotation,
            trackedImage.transform
        );

        // 初始化棋子控制器
        PieceController pc = piece.GetComponent<PieceController>();
        if (pc == null)
        {
            Debug.LogError("棋子Prefab缺少PieceController组件！");
            return;
        }
        pc.Initialize(trackedImage);

        // 根据图像尺寸缩放
        if (trackedImage.size.x <= 0)
        {
            Debug.LogError("图像尺寸无效！");
            return;
        }
        float scaleFactor = trackedImage.size.x / 0.05f;
        piece.transform.localScale = Vector3.one * scaleFactor;
    }

    /// <summary>
    /// 重置棋盘状态（旋转到默认角度）
    /// </summary>
    public void ResetBoard()
    {
        if (spawnedBoard == null) return;
        Quaternion targetRotation = Quaternion.Euler(90f, currentRotationY, 0);
        StartCoroutine(SmoothReset(
            spawnedBoard.transform.position,
            spawnedBoard.transform.localScale,
            targetRotation
        ));
    }

    /// <summary>
    /// 平滑重置协程
    /// </summary>
    IEnumerator SmoothReset(Vector3 position, Vector3 scale, Quaternion rotation)
    {
        isMoving = true;
        float duration = 0.5f;
        float elapsed = 0;

        Vector3 startPos = spawnedBoard.transform.position;
        Quaternion startRot = spawnedBoard.transform.rotation;
        Vector3 startScale = spawnedBoard.transform.localScale;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            spawnedBoard.transform.position = Vector3.Lerp(startPos, position, t);
            spawnedBoard.transform.rotation = Quaternion.Slerp(startRot, rotation, t);
            spawnedBoard.transform.localScale = Vector3.Lerp(startScale, scale, t);

            yield return null;
        }

        spawnedBoard.transform.position = position;
        spawnedBoard.transform.rotation = rotation;
        spawnedBoard.transform.localScale = scale;
        isMoving = false;
    }

    // ==================== 全局状态 ====================
    /// <summary>
    /// 棋盘是否存在（静态属性）
    /// </summary>
    public static bool BoardExists { get; private set; }

    void OnDestroy() => BoardExists = false;
}