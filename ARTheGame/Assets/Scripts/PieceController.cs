using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;

/// <summary>
/// 棋子控制器，负责管理棋子的AR追踪、放置逻辑和UI交互
/// 主要功能：
/// 1. 跟随AR标记进行平滑移动
/// 2. 检测与网格的碰撞并显示放置UI
/// 3. 处理棋子的最终放置确认/取消逻辑
/// 4. 维护棋子与游戏网格的交互状态
/// </summary>
public class PieceController : MonoBehaviour
{
    // ==================== 棋子类型 ====================
    public enum PieceType { None, O, X }
    [Tooltip("当前棋子的类型（O/X）")]
    public PieceType pieceType;

    // ==================== UI元素 ====================
    [Header("UI Elements")]
    [Tooltip("放置操作UI面板")]
    public GameObject placementUI;
    [Tooltip("确认放置按钮")]
    public Button confirmButton;
    [Tooltip("取消放置按钮")]
    public Button cancelButton;

    // ==================== 位置设置 ====================
    [Header("位置设置")]
    [Tooltip("相对于AR卡片的三维位置偏移（本地坐标系）")]
    public Vector3 positionOffset = new Vector3(0, 0.1f, 0);

    // ==================== 跟随设置 ====================
    [Header("跟随设置")]
    [Tooltip("位置跟随平滑系数（值越大跟随越快）")]
    public float followSmoothness = 25f;
    [Tooltip("位置更新最小阈值（小于此值不更新）")]
    public float positionThreshold = 0.005f;

    // ==================== 旋转设置 ====================
    [Header("角度设置")]
    [Tooltip("相对于AR卡片的旋转偏移（欧拉角）")]
    public Vector3 rotationOffset = Vector3.zero;

    // 私有变量
    private Transform targetCell;        // 当前目标网格单元
    private Transform cardTransform;     // 关联的AR卡片变换
    private bool isPlacing = true;       // 是否处于放置模式
    private Vector3 lastValidPosition;   // 最后有效位置（防抖动）

    void Start()
    {
        // 初始化UI状态
        placementUI.SetActive(false);
        // 绑定按钮事件
        confirmButton.onClick.AddListener(ConfirmPlacement);
        cancelButton.onClick.AddListener(CancelPlacement);
    }

    void Update()
    {
        if (isPlacing && cardTransform != null)
        {
            FollowCardWithStabilization(); // 持续跟随AR卡片
            UpdateUIOrientation();         // 更新UI朝向
        }
    }

    /// <summary>
    /// 带稳定效果的卡片跟随逻辑
    /// 实现特点：
    /// 1. 使用本地坐标系偏移
    /// 2. 包含位置稳定阈值防止微小抖动
    /// 3. 平滑旋转过渡
    /// </summary>
    void FollowCardWithStabilization()
    {
        // 计算基于本地坐标系的目标位置
        Vector3 targetPos = cardTransform.position + 
                          cardTransform.TransformDirection(positionOffset);

        // 计算目标旋转（应用旋转偏移）
        Quaternion targetRot = cardTransform.rotation * Quaternion.Euler(rotationOffset);

        // 位置稳定逻辑（防抖动）
        if (Vector3.Distance(transform.position, targetPos) > positionThreshold)
        {
            // 平滑插值移动
            transform.position = Vector3.Lerp(
                transform.position,
                targetPos,
                Time.deltaTime * followSmoothness
            );
            lastValidPosition = transform.position;
        }
        else
        {
            // 低于阈值时保持最后有效位置
            transform.position = lastValidPosition;
        }

        // 平滑旋转过渡
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRot,
            Time.deltaTime * followSmoothness
        );
    }

    /// <summary>
    /// 更新UI始终面向摄像机
    /// </summary>
    void UpdateUIOrientation()
    {
        if (Camera.main == null) return;
        // 计算朝向摄像机的方向
        Vector3 directionToCamera = Camera.main.transform.position - placementUI.transform.position;
        // 使UI反向面对摄像机（保证文字正向显示）
        placementUI.transform.rotation = Quaternion.LookRotation(-directionToCamera);
    }

    /// <summary>
    /// 碰撞进入检测（与网格单元交互）
    /// </summary>
    void OnTriggerEnter(Collider other)
    {
        if (!isPlacing || !other.CompareTag("GridCell")) return;
        
        targetCell = other.transform;
        placementUI.SetActive(true);
        // 将UI位置转换为屏幕空间
        placementUI.transform.position = 
            Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 0.2f);
    }

    /// <summary>
    /// 碰撞退出检测
    /// </summary>
    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("GridCell"))
        {
            placementUI.SetActive(false);
            targetCell = null; // 清除目标网格
        }
    }

    /// <summary>
    /// 确认放置操作
    /// 执行逻辑：
    /// 1. 绑定到目标网格单元
    /// 2. 更新网格状态
    /// 3. 销毁关联的AR卡片
    /// </summary>
    void ConfirmPlacement()
    {
        if (targetCell == null) return;
        
        // 设置父子关系
        transform.SetParent(targetCell);
        // 调整本地位置（轻微抬升防止穿插）
        transform.localPosition = Vector3.up * 0.05f;
        isPlacing = false;
        placementUI.SetActive(false);
        // 更新网格单元占用状态
        targetCell.GetComponent<GridCell>().SetOccupied(pieceType);
        // 销毁关联的AR追踪对象
        Destroy(cardTransform.gameObject);
    }

    /// <summary>
    /// 取消放置操作
    /// </summary>
    void CancelPlacement()
    {
        placementUI.SetActive(false);
        targetCell = null; // 重置目标网格
    }

    /// <summary>
    /// 初始化棋子与AR追踪图像的关联
    /// 关键步骤：
    /// 1. 保存卡片变换引用
    /// 2. 应用初始位置/旋转偏移
    /// 3. 记录初始位置用于稳定计算
    /// </summary>
    public void Initialize(ARTrackedImage trackedImage)
    {
        cardTransform = trackedImage.transform;
        // 应用本地坐标系的位置偏移
        transform.position = trackedImage.transform.position + 
                            trackedImage.transform.TransformDirection(positionOffset);
        // 应用组合旋转
        transform.rotation = trackedImage.transform.rotation * Quaternion.Euler(rotationOffset);
        // 初始化最后有效位置
        lastValidPosition = transform.position;
    }
}