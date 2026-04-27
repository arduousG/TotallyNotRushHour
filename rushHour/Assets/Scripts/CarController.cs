using UnityEngine;
using System.Collections.Generic;

public class CarController : MonoBehaviour
{
    public Vector2Int gridPosition;
    public bool isHorizontal;
    public int length;
    public float tileSpacing = 5f;
    public PuzzleController puzzle;
    public List<Vector2Int> occupiedCells = new List<Vector2Int>();

    private bool selected = false;

    private static CarController currentlySelected;

    public List<Vector2Int> GetOccupiedCells(Vector2Int origin)
    {
        List<Vector2Int> cells = new List<Vector2Int>();

        for (int i = 0; i < length; i++)
        {
            if (isHorizontal)
                cells.Add(new Vector2Int(origin.x + i, origin.y));
            else
                cells.Add(new Vector2Int(origin.x, origin.y + i));
        }
        return cells;
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Debug.Log("Mouse Clicked");
        }

        if (!selected)
            return;

        HandleMovement();
    }


    void HandleMovement()
    {
        if (isHorizontal)
        {
            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                Move(Vector2Int.right);
            }

            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                Move(Vector2Int.left);
            }
        }
        else
        {
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                Move(Vector2Int.up);
            }

            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                Move(Vector2Int.down);
            }
        }
    }

    void Move(Vector2Int direction)
    {
        Vector2Int newOrigin = gridPosition + direction;

        if (!puzzle.CanPlaceCar(this, newOrigin))
            return;

        puzzle.SetCarPosition(this, gridPosition, newOrigin);

        gridPosition = newOrigin;
        transform.position = GridToWorld(gridPosition);
    }

    void OnMouseDown()
    {
        if (currentlySelected != null)
        {
            currentlySelected.selected = false;
        }

        selected = true;
        currentlySelected = this;

        Debug.Log("Car Selected");
    }

    Vector3 GridToWorld(Vector2Int gridPosition)
    {
        float xOffset = tileSpacing / 2f;
        float zOffset = tileSpacing / 2f;

        if (isHorizontal)
        {
            xOffset += (length - 1) * tileSpacing / 2f;
        }
        else
        {
            zOffset += (length - 1) * tileSpacing / 2f;
        }

        return new Vector3(
            gridPosition.x * tileSpacing + xOffset,
            0,
            gridPosition.y * tileSpacing + zOffset
        );
    }
}
