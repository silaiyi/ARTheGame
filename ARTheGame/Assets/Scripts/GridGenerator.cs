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
        // 清空现有棋盘格
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }

        // 计算起始偏移量
        float offset = (gridSize - 1) * cellSize * 0.5f;

        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                // 计算本地坐标系位置
                Vector3 localPosition = new Vector3(
                    x * cellSize - offset,
                    0,
                    y * cellSize - offset
                );

                // 实例化并设置父物体
                GameObject cell = Instantiate(
                    gridCellPrefab,
                    transform  // 直接设置父物体
                );

                // 设置本地坐标和旋转
                cell.transform.localPosition = localPosition;
                cell.transform.localRotation = Quaternion.Euler(90, 0, 0);
                cell.name = $"Cell_{x}_{y}";
            }
        }
    }
    public Transform GetCell(int x, int y)
    {
        return transform.Find($"Cell_{x}_{y}");
    }
}