using UnityEngine;

public class CarController : MonoBehaviour
{
    public Vector2Int gridPosition;
    public bool isHorizontal;
    public int length;

    public float tileSpacing = 5f;

    private bool selected = false;

    private static CarController currentlySelected;

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
        gridPosition += direction;

        Vector3 worldPosition = GridToWorld(gridPosition);

        transform.position = worldPosition;
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
