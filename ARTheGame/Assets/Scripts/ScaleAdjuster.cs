using UnityEngine;

[ExecuteAlways]
public class ScaleAdjuster : MonoBehaviour
{
    [Header("Scale Settings")]
    public Vector3 baseScale = Vector3.one; // 明确定义baseScale字段
    [Range(0.1f, 3f)]
    public float scaleMultiplier = 1f;

    void Update()
    {
        // 确保在编辑器模式下实时预览
        #if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            transform.localScale = baseScale * scaleMultiplier;
            return;
        }
        #endif
        
        transform.localScale = baseScale * scaleMultiplier;
    }
}