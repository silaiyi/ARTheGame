using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GridGenerator : MonoBehaviour
{
    [Tooltip("拖入上一步制作的GridCell预制件")]
    public GameObject gridCellPrefab;  // 格子预制件引用
    
    [Range(3, 7), Tooltip("棋盘尺寸（5x5）")]
    public int gridSize = 5;           // 控制网格大小
    
    [Tooltip("每个格子的物理尺寸（单位：米）")]
    public float cellSize = 0.2f;      // 控制格子间距
    
    void Start()
    {
        GenerateGrid();
    }

    public void GenerateGrid()
    {
        // 获取父物体当前位置
        Vector3 parentPosition = transform.position;
        
        // 计算起始偏移量（基于父物体坐标系）
        float offset = (gridSize - 1) * cellSize * 0.5f;
        
        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                // 计算相对父物体的本地位置
                Vector3 localPos = new Vector3(
                    x * cellSize - offset,
                    0,
                    y * cellSize - offset
                );

                // 转换为世界坐标（考虑父物体位置和旋转）
                Vector3 worldPos = transform.TransformPoint(localPos);
                
                GameObject cell = Instantiate(
                    gridCellPrefab,
                    worldPos,  // 使用转换后的世界坐标
                    Quaternion.Euler(90, 0, 0),
                    transform
                );
                
                cell.name = $"Cell_{x}_{y}";
            }
        }
    }
}