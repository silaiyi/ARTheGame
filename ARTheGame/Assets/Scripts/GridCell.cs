using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GridCell : MonoBehaviour
{
    public PieceController.PieceType occupiedType = PieceController.PieceType.None;
    
    public void SetOccupied(PieceController.PieceType type)
    {
        occupiedType = type;
        GetComponent<Collider>().enabled = false;
    }
}
