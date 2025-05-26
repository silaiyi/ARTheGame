using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.UI;

public class ImageTracker : MonoBehaviour
{
    [Header("AR Settings")]
    public GameObject chessboardPrefab;
    public float defaultDistance = 1.0f; // 默认生成距离
    public float rotationSpeed = 20f;    // 旋转速度
    public float minScale = 0.1f;        // 最小缩放
    public float maxScale = 2.0f;        // 最大缩放

    [Header("UI Elements")]
    public GameObject confirmationUI;
    public RectTransform confirmationButton;

    private ARTrackedImageManager trackedImageManager;
    private GameObject spawnedBoard;
    private Camera arCamera;
    private bool isInteracting;
    private Vector2 lastTouchPosition;
    private float initialDistance;
    private ARTrackedImage currentTrackedImage;
    [Header("Reposition Settings")]
    public Button repositionButton; // 新增的重新定位按钮
    public float moveDuration = 0.5f; // 移动动画时长
    public float viewportMargin = 0.1f; // 屏幕边缘留白

    private Vector3 targetPosition;
    private Quaternion targetRotation;
    private bool isMoving = false;

    [Header("Interaction")]
    public float scaleSensitivity = 0.01f;
    private ARTrackedImageManager imageManager;
    private GameObject currentBoard;
    private Vector2 touchStartPos;
    private float currentRotationY;
    [Header("Viewport Settings")]
    [Tooltip("垂直方向的位置偏移（0-1）")]
    public float verticalPositionFactor = 0.25f; // 可通过Inspector调整

    [Tooltip("距离摄像头的基准偏移")]
    public float cameraDistanceOffset = 0.8f; // 可直观调整

    [Tooltip("地面检测射线长度")]
    public float groundCheckDistance = 2f; // 在Inspector中可视化管理
    [Header("Rotation Settings")]
    public float maxVerticalAngle = 90f; // 新增垂直旋转角度限制
    private float currentRotationX; // 新增X轴旋转存储
    [Header("Spawn Settings")]
    [Tooltip("初始生成旋转偏移")]
    public Vector3 spawnRotationOffset = new Vector3(0, 90f, 0);


    void Awake()
    {
        trackedImageManager = GetComponent<ARTrackedImageManager>();
        arCamera = GetComponentInChildren<Camera>();
        if (repositionButton != null)
        {
            repositionButton.gameObject.SetActive(false); // 初始隐藏
            repositionButton.onClick.RemoveAllListeners(); // 清除旧监听
            repositionButton.onClick.AddListener(MoveBoardToView);
        }
    }
    void Update()
    {
        if (!spawnedBoard || isMoving) return;
        if (Input.touchCount == 0)
        {
            touchStartPos = Vector2.zero;
        }
        HandleTouchInput();

    }

    void OnEnable()
    {
        trackedImageManager.trackedImagesChanged += OnTrackedImagesChanged;
    }

    void OnDisable()
    {
        trackedImageManager.trackedImagesChanged -= OnTrackedImagesChanged;
    }

    void OnTrackedImagesChanged(ARTrackedImagesChangedEventArgs eventArgs)
    {
        foreach (var trackedImage in eventArgs.added)
        {
            UpdateTrackedImage(trackedImage);
        }

        foreach (var trackedImage in eventArgs.updated)
        {
            UpdateTrackedImage(trackedImage);
        }
    }

    void UpdateTrackedImage(ARTrackedImage trackedImage)
    {
        if (trackedImage.trackingState == TrackingState.Tracking)
        {
            currentTrackedImage = trackedImage;
            ShowConfirmationUI(trackedImage.transform.position);
        }
    }

    void ShowConfirmationUI(Vector3 worldPosition)
    {
        // 将世界坐标转换为屏幕坐标
        Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPosition);
        confirmationButton.position = screenPos;
        confirmationUI.SetActive(true);
    }

    public void ConfirmPlacement()
    {
        if (arCamera == null) // 新增空引用检查
        {
            Debug.LogError("AR Camera not found!");
            return;
        }
        // 生成棋盘在摄像头前方
        Vector3 spawnPosition = arCamera.transform.position
            + arCamera.transform.forward * defaultDistance;
            //+ new Vector3(0, 0.1f, 0); // 防止棋盘嵌入地面
        Quaternion spawnRotation = Quaternion.LookRotation(
            arCamera.transform.forward, 
            Vector3.up
        ) * Quaternion.Euler(spawnRotationOffset);
        if (repositionButton != null)
        {
            repositionButton.gameObject.SetActive(true);
            // 设置按钮位置
            RepositionButtonToCorner();
        }

        if (spawnedBoard != null) Destroy(spawnedBoard);

        spawnedBoard = Instantiate(chessboardPrefab, spawnPosition, spawnRotation);
        confirmationUI.SetActive(false);
        currentBoard = spawnedBoard;
        currentRotationY = spawnRotation.eulerAngles.y;
        currentRotationX = 0; // 重置垂直角度

        // 禁用图像追踪
        trackedImageManager.enabled = false;
        // 在ConfirmPlacement方法最后添加：
        spawnedBoard.AddComponent<ARAnchor>();  // 保持AR空间位置稳定
        spawnedBoard.AddComponent<Rigidbody>().isKinematic = true;  // 防止物理飞走
    }
    /*
    void HandleTouchInput()
    {
        // 双指缩放
        if (Input.touchCount == 2)
        {
            Touch touch1 = Input.GetTouch(0);
            Touch touch2 = Input.GetTouch(1);

            if (touch2.phase == TouchPhase.Began)
            {
                initialDistance = Vector2.Distance(touch1.position, touch2.position);
            }

            if (touch1.phase == TouchPhase.Moved || touch2.phase == TouchPhase.Moved)
            {
                float currentDistance = Vector2.Distance(touch1.position, touch2.position);
                float scaleFactor = currentDistance / initialDistance;

                Vector3 newScale = spawnedBoard.transform.localScale * scaleFactor;
                newScale = Vector3.ClampMagnitude(newScale, maxScale);
                newScale = Vector3.Max(newScale, Vector3.one * minScale);

                spawnedBoard.transform.localScale = newScale;
                initialDistance = currentDistance;
            }
        }
    }*/
    void HandleTouchInput()
    {
        if (Input.touchCount == 1) HandleRotation();
        if (Input.touchCount == 2) HandleZoom();
    }

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
                // 获取标准化位移量
                Vector2 delta = touch.deltaPosition * rotationSpeed * Time.deltaTime;
                
                // 修正水平旋转方向（乘以-1反转方向）
                currentRotationY -= delta.x; 
                
                // 新增垂直旋转控制（使用delta.y）
                currentRotationX = Mathf.Clamp(
                    currentRotationX + delta.y, 
                    -maxVerticalAngle, 
                    maxVerticalAngle
                );
                
                // 应用复合旋转（先Y轴后X轴）
                currentBoard.transform.rotation = Quaternion.Euler(
                    currentRotationX,
                    currentRotationY,
                    0
                );
                break;
        }
    }

    void HandleZoom()
    {
        if (currentBoard == null) return;

        Touch touch0 = Input.GetTouch(0);
        Touch touch1 = Input.GetTouch(1);

        // 获取两指当前位置和上一帧位置
        Vector2 touch0PrevPos = touch0.position - touch0.deltaPosition;
        Vector2 touch1PrevPos = touch1.position - touch1.deltaPosition;

        // 计算前后两指距离差
        float prevTouchDelta = Vector2.Distance(touch0PrevPos, touch1PrevPos);
        float currentTouchDelta = Vector2.Distance(touch0.position, touch1.position);

        // 计算缩放系数
        float scaleDelta = (currentTouchDelta - prevTouchDelta) * scaleSensitivity;
        Vector3 newScale = currentBoard.transform.localScale + (Vector3.one * scaleDelta);

        // 应用缩放限制
        newScale.x = Mathf.Clamp(newScale.x, minScale, maxScale);
        newScale.y = Mathf.Clamp(newScale.y, minScale, maxScale);
        newScale.z = Mathf.Clamp(newScale.z, minScale, maxScale);

        currentBoard.transform.localScale = newScale;
    }
    void MoveBoardToView()
    {
        if (spawnedBoard == null || arCamera == null) return;

        StartCoroutine(SmoothMoveToCameraView());
    }
    IEnumerator SmoothMoveToCameraView()
    {
        isMoving = true;

        // 计算目标位置和旋转
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

            yield return new WaitForEndOfFrame();
            if (Physics.Raycast(spawnedBoard.transform.position + Vector3.up,
                Vector3.down, out var finalHit, 2f))
            {
                spawnedBoard.transform.position = finalHit.point + Vector3.up * 0.05f;
            }
        }

        // 最终位置修正
        spawnedBoard.transform.position = targetPosition;
        spawnedBoard.transform.rotation = targetRotation;
        spawnedBoard.transform.localScale = Vector3.one;

        isMoving = false;
    }
    void CalculateOptimalPosition(out Vector3 position, out Quaternion rotation)
    {
        // 使用可调参数计算视口点
        Vector3 viewportPoint = new Vector3(
            0.5f,
            verticalPositionFactor, // 使用可调参数控制垂直位置
            arCamera.nearClipPlane + cameraDistanceOffset // 使用基准偏移
        );

        // 转换为世界坐标
        position = arCamera.ViewportToWorldPoint(viewportPoint);
        rotation = Quaternion.LookRotation(arCamera.transform.forward, Vector3.up);

        // 增强版地面检测
        RaycastHit hit;
        Vector3 rayStart = position + Vector3.up * 1f; // 从上方开始检测
        if (Physics.Raycast(rayStart, Vector3.down, out hit, groundCheckDistance))
        {
            position.y = hit.point.y + 0.05f;
            Debug.DrawLine(rayStart, hit.point, Color.green, 2f); // 调试射线
        }
        else
        {
            position.y = arCamera.transform.position.y - 0.3f; // 基于相机高度修正
        }
    }
    void RepositionButtonToCorner()
    {
        if (repositionButton == null) return;

        RectTransform rectTransform = repositionButton.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            // 使用锚点定位（左上角）
            rectTransform.anchorMin = new Vector2(0, 1);
            rectTransform.anchorMax = new Vector2(0, 1);
            rectTransform.anchoredPosition = new Vector2(200, -100); // 适当调整边距
        }
    }
    
}
