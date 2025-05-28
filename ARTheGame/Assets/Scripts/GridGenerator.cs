using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 网格生成器，负责动态创建棋盘网格
/// 主要功能：
/// 1. 根据指定参数生成N×N的网格
/// 2. 支持动态重建网格
/// 3. 提供按坐标访问网格单元的方法
/// </summary>
public class GridGenerator : MonoBehaviour
{
    // ==================== 网格设置 ====================
    [Header("网格设置")]
    [Tooltip("网格单元预制体（必须包含GridCell组件）")]
    public GameObject gridCellPrefab;  // 格子预制件引用
    
    [Range(3, 7), Tooltip("棋盘尺寸（N×N）")]
    public int gridSize = 5;           // 控制网格大小
    
    [Tooltip("每个格子的物理尺寸（单位：米）")]
    public float cellSize = 0.2f;      // 控制格子间距
    
    void Start()
    {
        GenerateGrid(); // 游戏开始时自动生成网格
    }

    /// <summary>
    /// 生成网格
    /// 执行步骤：
    /// 1. 清理现有网格
    /// 2. 计算网格偏移量
    /// 3. 循环创建网格单元
    /// 4. 设置单元位置和名称
    /// </summary>
    public void GenerateGrid()
    {
        // 清理现有网格（防止重复生成）
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }

        // 计算中心偏移量（使网格居中）
        float offset = (gridSize - 1) * cellSize * 0.5f;

        // 双层循环创建网格
        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                // 计算本地坐标系位置（基于父物体）
                Vector3 localPosition = new Vector3(
                    x * cellSize - offset, // X轴位置
                    0,                     // Y轴位置（地面高度）
                    y * cellSize - offset  // Z轴位置
                );

                // 实例化网格单元
                GameObject cell = Instantiate(
                    gridCellPrefab,
                    transform  // 设置父物体为当前对象
                );

                // 设置单元属性
                cell.transform.localPosition = localPosition; // 本地位置
                cell.transform.localRotation = Quaternion.Euler(90, 0, 0); // 旋转（水平放置）
                cell.name = $"Cell_{x}_{y}"; // 按坐标命名（便于查找）
            }
        }
    }

    /// <summary>
    /// 获取指定坐标的网格单元
    /// </summary>
    /// <param name="x">X坐标（0到gridSize-1）</param>
    /// <param name="y">Y坐标（0到gridSize-1）</param>
    /// <returns>网格单元的Transform，未找到时返回null</returns>
    public Transform GetCell(int x, int y)
    {
        // 按命名规范查找网格单元
        return transform.Find($"Cell_{x}_{y}");
    }
}