using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;


public class PieceController : MonoBehaviour
{
    public enum PieceType { None, O, X }
    public PieceType pieceType;

    [Header("UI Elements")]
    public GameObject placementUI;
    public Button confirmButton;
    public Button cancelButton;

    [Header("位置设置")]
    [Tooltip("相对于卡片的位移偏移（XYZ坐标系）")]
    public Vector3 positionOffset = new Vector3(0, 0.1f, 0); // 替换原来的verticalOffset

    [Header("跟随设置")]
    [Tooltip("跟随平滑速度")]
    public float followSmoothness = 25f;
    [Tooltip("移动更新阈值")]
    public float positionThreshold = 0.005f;

    [Header("角度设置")]
    [Tooltip("相对于卡片的旋转偏移（欧拉角）")]
    public Vector3 rotationOffset = Vector3.zero;

    private Transform targetCell;
    private Transform cardTransform;
    private bool isPlacing = true;
    private Vector3 lastValidPosition;

    void Start()
    {
        placementUI.SetActive(false);
        confirmButton.onClick.AddListener(ConfirmPlacement);
        cancelButton.onClick.AddListener(CancelPlacement);
    }

    void Update()
    {
        if (isPlacing && cardTransform != null)
        {
            FollowCardWithStabilization();
            UpdateUIOrientation();
        }
    }

    void FollowCardWithStabilization()
    {
        // 计算目标位置（使用三维偏移）
        Vector3 targetPos = cardTransform.position + 
                          cardTransform.TransformDirection(positionOffset); // 关键修改

        // 计算目标旋转
        Quaternion targetRot = cardTransform.rotation * Quaternion.Euler(rotationOffset);

        // 位置稳定逻辑
        if (Vector3.Distance(transform.position, targetPos) > positionThreshold)
        {
            transform.position = Vector3.Lerp(
                transform.position,
                targetPos,
                Time.deltaTime * followSmoothness
            );
            lastValidPosition = transform.position;
        }
        else
        {
            transform.position = lastValidPosition;
        }

        // 旋转稳定逻辑
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRot,
            Time.deltaTime * followSmoothness
        );
    }

    void UpdateUIOrientation()
    {
        if (Camera.main == null) return;
        Vector3 directionToCamera = Camera.main.transform.position - placementUI.transform.position;
        placementUI.transform.rotation = Quaternion.LookRotation(-directionToCamera);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!isPlacing || !other.CompareTag("GridCell")) return;
        targetCell = other.transform;
        placementUI.SetActive(true);
        placementUI.transform.position = 
            Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 0.2f);
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("GridCell"))
        {
            placementUI.SetActive(false);
            targetCell = null;
        }
    }

    void ConfirmPlacement()
    {
        if (targetCell == null) return;
        transform.SetParent(targetCell);
        transform.localPosition = Vector3.up * 0.05f;
        isPlacing = false;
        placementUI.SetActive(false);
        targetCell.GetComponent<GridCell>().SetOccupied(pieceType);
        Destroy(cardTransform.gameObject);
    }

    void CancelPlacement()
    {
        placementUI.SetActive(false);
        targetCell = null;
    }

    public void Initialize(ARTrackedImage trackedImage)
    {
        cardTransform = trackedImage.transform;
        // 使用三维偏移初始化位置
        transform.position = trackedImage.transform.position + 
                            trackedImage.transform.TransformDirection(positionOffset); // 关键修改
        transform.rotation = trackedImage.transform.rotation * Quaternion.Euler(rotationOffset);
        lastValidPosition = transform.position;
    }
}