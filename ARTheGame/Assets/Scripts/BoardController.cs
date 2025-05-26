using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

public class BoardController : MonoBehaviour
{
    [Header("生成设置")]
    public GameObject chessboardPrefab;
    public float spawnDistance = 1.5f; // 距离摄像机的距离
    public Vector3 spawnOffset = new Vector3(0, -0.5f, 0); // 垂直偏移
    
    [Header("旋转设置")]
    public float rotationSpeed = 0.5f;
    public bool allowRotation = true;
    
    private GameObject currentBoard;
    private ARSessionOrigin arOrigin;
    private Vector2 touchStartPos;
    [Range(0, 360)]
    public float maxRotationAngle = 180f; // 允许旋转的最大角度

    private float currentRotationY;
    public float scaleSensitivity = 0.01f;

    void Start()
    {
        arOrigin = FindObjectOfType<ARSessionOrigin>();
    }

    // 由确认按钮调用
    public void SpawnChessboard()
    {
        if(currentBoard != null) return;
        
        Transform cameraTransform = arOrigin.camera.transform;
        Vector3 spawnPos = cameraTransform.position + 
                          cameraTransform.forward * spawnDistance +
                          spawnOffset;
        
        currentBoard = Instantiate(chessboardPrefab, 
            spawnPos, 
            Quaternion.LookRotation(-cameraTransform.forward));
        
        EnableARDetection(false); // 关闭AR追踪
    }

    void Update()
    {
        if(!allowRotation || currentBoard == null) return;

        HandleTouchRotation();
    }

    void HandleTouchRotation()
    {
        if (Input.touchCount == 1)
        {
            Touch touch = Input.GetTouch(0);

            switch (touch.phase)
            {
                case TouchPhase.Began:
                    touchStartPos = touch.position;
                    break;

                case TouchPhase.Moved:
                    Vector2 delta = touch.position - touchStartPos;
                    currentBoard.transform.Rotate(
                        Vector3.up,
                        -delta.x * rotationSpeed * Time.deltaTime,
                        Space.World
                    );
                    float targetRotation = currentRotationY + (-delta.x * rotationSpeed);
                    targetRotation = Mathf.Clamp(targetRotation, -maxRotationAngle, maxRotationAngle);
                    
                    currentBoard.transform.rotation = Quaternion.Euler(
                        0, 
                        targetRotation, 
                        0
                    );                    
                    currentRotationY = targetRotation;
        
                    touchStartPos = touch.position;
                    break;
            }

        }
        
        
    }
    void HandlePinchZoom()
    {
        if(Input.touchCount == 2)
        {
            Touch touch0 = Input.GetTouch(0);
            Touch touch1 = Input.GetTouch(1);
            
            Vector2 prevPos0 = touch0.position - touch0.deltaPosition;
            Vector2 prevPos1 = touch1.position - touch1.deltaPosition;
            
            float prevDistance = Vector2.Distance(prevPos0, prevPos1);
            float currentDistance = Vector2.Distance(touch0.position, touch1.position);
            
            float scaleFactor = (currentDistance - prevDistance) * scaleSensitivity;
            currentBoard.transform.localScale += 
                Vector3.one * scaleFactor;
        }
    }

    void EnableARDetection(bool state)
    {
        ARTrackedImageManager imageManager = 
            arOrigin.GetComponent<ARTrackedImageManager>();
        if(imageManager != null)
            imageManager.enabled = state;
    }
}
