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
    public float minScale = 0.5f;        // 最小缩放
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
            + arCamera.transform.forward * defaultDistance
            + new Vector3(0, 0.1f, 0); // 防止棋盘嵌入地面
        Quaternion spawnRotation = Quaternion.LookRotation(
            arCamera.transform.forward,
            Vector3.up
        );
        if (repositionButton != null)
        {
            repositionButton.gameObject.SetActive(true);
            // 设置按钮位置
            RepositionButtonToCorner();
        }

        if (spawnedBoard != null) Destroy(spawnedBoard);

        spawnedBoard = Instantiate(chessboardPrefab, spawnPosition, spawnRotation);
        confirmationUI.SetActive(false);

        // 禁用图像追踪
        trackedImageManager.enabled = false;
        // 在ConfirmPlacement方法最后添加：
        spawnedBoard.AddComponent<ARAnchor>();  // 保持AR空间位置稳定
        spawnedBoard.AddComponent<Rigidbody>().isKinematic = true;  // 防止物理飞走
    }
    void Update()
    {
        if (!spawnedBoard || isMoving) return;

        HandleTouchInput();

    }
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

            yield return null;
        }

        // 最终位置修正
        spawnedBoard.transform.position = targetPosition;
        spawnedBoard.transform.rotation = targetRotation;
        spawnedBoard.transform.localScale = Vector3.one;

        isMoving = false;
    }
    void CalculateOptimalPosition(out Vector3 position, out Quaternion rotation)
    {
        // 调整视口参数
        Vector3 viewportPoint = new Vector3(
            0.5f,  // 水平居中
            0.3f,  // 垂直位置下移（防止被遮挡）
            arCamera.nearClipPlane + defaultDistance + 0.5f // 增加距离
        );

        // 转换为世界坐标
        position = arCamera.ViewportToWorldPoint(viewportPoint);
        rotation = Quaternion.LookRotation(arCamera.transform.forward, Vector3.up);

        // 添加地面检测
        RaycastHit hit;
        if (Physics.Raycast(position, Vector3.down, out hit, 1f))
        {
            position.y = hit.point.y + 0.05f; // 保持离地面一定高度
        }
        else
        {
            position.y = Mathf.Max(position.y, 0.1f);
        }
    }
    void RepositionButtonToCorner()
    {
        if (repositionButton == null) return;
        
        RectTransform rectTransform = repositionButton.GetComponent<RectTransform>();
        if(rectTransform != null)
        {
            // 使用锚点定位（左上角）
            rectTransform.anchorMin = new Vector2(0, 1);
            rectTransform.anchorMax = new Vector2(0, 1);
            rectTransform.anchoredPosition = new Vector2(100, -100); // 适当调整边距
        }
    }
}
