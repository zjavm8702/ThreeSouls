#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using ThreeSouls.Combat;
using ThreeSouls.Interface;
using ThreeSouls.Utility;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

public class T_Astar : MonoBehaviour, IPathGrid
{
    [SerializeField] Tilemap Map;
    [SerializeField] GameObject S_Object;
    [SerializeField] GameObject E_Object;
    [SerializeField] Button S_Button;
    [SerializeField] Button R_Button;

    List<PathNode> NodeContainer = new List<PathNode>();
    AStarPathfinder AStar;

    private int Index = 1;
    private bool IsPlay = false;

    void Start()
    {
        AStar = new AStarPathfinder(this);

        S_Button.onClick.AddListener(() =>
        {
            Find();
            IsPlay = true;
        });

        R_Button.onClick.AddListener(() =>
        {
            IsPlay = false;
        });
    }

    void Reset()
    {
        IsPlay = false;
        Index = 1;
        NodeContainer.Clear();
    }

    // Update is called once per frame
    void Update()
    {
        if (IsPlay == false)
        {
            return;
        }

        if (NodeContainer == null)
        {
            return;
        }

        if (NodeContainer.Count <= Index)
        {
            return;
        }

        var targetPosition = CellToWorldCenter(NodeContainer[Index].CellPosition);
        var pos = Vector3.MoveTowards(S_Object.transform.position, targetPosition, 10 * Time.deltaTime);
        S_Object.transform.position = pos;

        var distance = Vector2.Distance(S_Object.transform.position, targetPosition);

        if (distance <= 0.001f)
        {
            S_Object.transform.position = targetPosition;
            Index++;
        }
    }

    void Find()
    {
        var start = new PathNode(WorldToCell(S_Object.transform.position));

        var goal = new PathNode(WorldToCell(E_Object.transform.position));

        NodeContainer = AStar.FindPath(start, goal);
    }

    private Vector3 CellToWorldCenter(Vector2Int point)
    {
        return Map.GetCellCenterWorld((Vector3Int)point);
    }

    private Vector2Int WorldToCell(Vector3 pos)
    {
        return (Vector2Int)Map.WorldToCell(pos);
    }

    public bool IsWalkable(Vector2Int pos)
    {
        return Map.HasTile((Vector3Int)pos);
    }

    public PathNode GetNodeData(Vector2Int pos)
    {
        if (IsWalkable(pos) == false)
        {
            return null;
        }

        return new PathNode(pos);
    }
}
#endif
