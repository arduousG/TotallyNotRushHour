using UnityEngine;
using System.Collections.Generic;
using UnityEngine.EventSystems;

public class CarController : MonoBehaviour
{
    public Vector2Int gridPosition;
    public bool isHorizontal;
    public int length;
    public bool isMainCar;
    public float tileSpacing = 5f;
    public PuzzleController puzzle;
    public List<Vector2Int> occupiedCells = new List<Vector2Int>();

    private Vector3 dragStartMousePos;
    private bool dragging = false;

    private bool selected = false;
    private float lastInvalidMoveSfxTime = -999f;
    private const float InvalidMoveSfxCooldown = 0.12f;

    private static CarController currentlySelected;
    public static CarController CurrentSelected
    {
        get { return currentlySelected; } //ADDed: with / for move highlighting, can be used for other selection based feats.
    }

    public static void ClearSel() // helper to clear stale static ref on reload without needing to click a car
    {
        if (currentlySelected != null)
        {
            currentlySelected.selected = false; //ADDed: w/ move highlighting
        }
        currentlySelected = null;
    }

    public void GetOccupiedCells(Vector2Int origin, List<Vector2Int> results)
{
    results.Clear();

    for (int i = 0; i < length; i++)
    {
        if (isHorizontal)
        {
            results.Add(new Vector2Int(origin.x + i, origin.y));
        }
        else
        {
            results.Add(new Vector2Int(origin.x, origin.y + i));
        }
    }
}

    void Update()
    {
        if (puzzle != null && puzzle.IsGameWon)
            return;

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
        {
            if (Time.time - lastInvalidMoveSfxTime >= InvalidMoveSfxCooldown)
            {
                AudioManager invalidMoveAudioManager = AudioManager.Instance;
                if (invalidMoveAudioManager != null)
                {
                    invalidMoveAudioManager.PlayInvalidMove();
                }
                lastInvalidMoveSfxTime = Time.time;
            }
            return;
        }

        puzzle.SetCarPosition(this, gridPosition, newOrigin);

        gridPosition = newOrigin;
        transform.position = GridToWorld(gridPosition);

        puzzle.RegisterMove(this, direction); // group repeated steps on one car/direction as one solution-style move

        puzzle.CheckWin(this);
        if (puzzle.IsGameWon)
        {
            return;
        }

        AudioManager moveSuccessAudioManager = AudioManager.Instance;
        if (moveSuccessAudioManager != null)
        {
            moveSuccessAudioManager.PlayMoveSuccess();
        }
    }

    void OnMouseDown()
    {
        if (IsPointerOverUi())
        {
            return;
        }

        if (currentlySelected != null)
        {
            currentlySelected.selected = false;

            CarView oldView = currentlySelected.GetComponent<CarView>();
            if (oldView != null)
            {
                oldView.SetSelected(false);
            }
        }

        selected = true;
        currentlySelected = this;

        CarView newView = GetComponent<CarView>();
        if (newView != null)
        {
            newView.SetSelected(true);
        }

        AudioManager audioManager = AudioManager.Instance;
        if (audioManager != null)
        {
            audioManager.PlayCarSelect();
        }

        dragStartMousePos = Input.mousePosition;

        Debug.Log("Car Selected");
    }

    private void OnDestroy()
    {
        //prevents stale static ref when car is removed on reload
        if (currentlySelected == this)
        {
            currentlySelected = null;
        }
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

    void OnMouseDrag()
    {
        if(puzzle != null && puzzle.IsGameWon)
            return;

        if (IsPointerOverUi())
        {
            return;
        }
        
        Vector3 mouseDelta = Input.mousePosition - dragStartMousePos;

        if (isHorizontal)
        {
            if (mouseDelta.x > 50f)
            {
                Move(Vector2Int.right);
                dragStartMousePos = Input.mousePosition;
            }
            else if (mouseDelta.x < -50f)
            {
                Move(Vector2Int.left);
                dragStartMousePos = Input.mousePosition;
            }
        }
        else
        {
            if (mouseDelta.y > 50f)
            {
                Move(Vector2Int.up);
                dragStartMousePos = Input.mousePosition;
            }
            else if (mouseDelta.y < -50f)
            {
                Move(Vector2Int.down);
                dragStartMousePos = Input.mousePosition;
            }
        }
    }

    bool IsPointerOverUi()
    {
        if (EventSystem.current == null)
        {
            return false;
        }

        if (Input.touchCount > 0)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                if (EventSystem.current.IsPointerOverGameObject(Input.GetTouch(i).fingerId))
                {
                    return true;
                }
            }
        }

        return EventSystem.current.IsPointerOverGameObject();
    }
}
