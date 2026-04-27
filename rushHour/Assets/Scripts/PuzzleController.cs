using UnityEngine;
using System.Collections.Generic;

public class PuzzleController : MonoBehaviour
{
    public GameObject boardTilePrefab;
    public GameObject carPrefab;
    public GameObject exitPrefab;
    public GameObject winText;

    public CarSpawnData[] cars;

    public int boardWidth = 6;
    public int boardHeight = 6;

    public float tileSpacing = 5f;

    private bool gameWon = false;
    public bool IsGameWon => gameWon;

    private CarController[,] grid;
    private Dictionary<CarController, Vector2Int> startingPositions = 
        new Dictionary<CarController, Vector2Int>();

    // Puzzle Definition
    [System.Serializable]
    public class CarSpawnData
    {
        public int carId;
        public bool isMainCar;
        public bool isHorizontal;
        public int length = 2;

        public Vector2Int gridPosition;
    }

    void Start()
    {
        GenerateBoard();
        grid = new CarController[boardWidth, boardHeight];
        SpawnCars();
        SpawnExit();

        AudioManager audioManager = AudioManager.Instance;
        if (audioManager != null)
        {
            audioManager.PlayMusicLoop();
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            ResetPuzzle();
        }
    }

    // Generating a 6x6 board
    void GenerateBoard()
    {
        for (int x = 0; x < boardWidth; x++)
        {
            for (int y = 0; y < boardHeight; y++)
            {
                Vector3 position = new Vector3(
                    x * tileSpacing + tileSpacing / 2f,
                    0,
                    y * tileSpacing + tileSpacing / 2f
                );

                Instantiate(boardTilePrefab, position, Quaternion.identity);
            }
        }
    }

    Vector3 GridToWorld(Vector2Int gridPosition, bool isHorizontal, int length)
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

    void SpawnCars()
    {
        foreach (CarSpawnData car in cars)
        {
            Vector3 worldPosition = GridToWorld(
                car.gridPosition,
                car.isHorizontal,
                car.length);

            GameObject spawnedCar = Instantiate(
                carPrefab,
                worldPosition,
                Quaternion.identity
            );

            CarView view = spawnedCar.GetComponent<CarView>();
            if (view != null)
            {
                view.Initialize(
                    car.carId,
                    car.isMainCar,
                    car.length,
                    car.isHorizontal
                );
            }

            CarController controller = spawnedCar.GetComponent<CarController>();
            if (controller != null)
            {
                controller.puzzle = this;
                controller.gridPosition = car.gridPosition;
                controller.isHorizontal = car.isHorizontal;
                controller.length = car.length;
                controller.tileSpacing = tileSpacing;
                controller.isMainCar = car.isMainCar;

                startingPositions[controller] = car.gridPosition;

                foreach (var cell in controller.GetOccupiedCells(car.gridPosition))
                {
                    if (IsInsideBoard(cell))
                    {
                        grid[cell.x, cell.y] = controller;
                    }
                }
            }
        }
    }

    public bool IsInsideBoard(Vector2Int pos)
    {
        return pos.x >= 0 && pos.x < boardWidth && pos.y >= 0 && pos.y < boardHeight;
    }

    public bool IsOccupied(Vector2Int pos)
    {
        return grid[pos.x, pos.y] != null;
    }

    public void UpdateGrid(Vector2Int oldPos, Vector2Int newPos, CarController car)
    {
        grid[oldPos.x, oldPos.y] = null;
        grid[newPos.x, newPos.y] = car;
    }

    public bool  CanPlaceCar(CarController car, Vector2Int newOrigin)
    {
        var cells = car.GetOccupiedCells(newOrigin);

        foreach (var cell in cells)
        {
            if (!IsInsideBoard(cell))
            {
                if (!IsExitCell(cell, car))
                    return false;
                    continue;
            }

            CarController occupyingCar = grid[cell.x, cell.y];
            if (occupyingCar != null && occupyingCar != car)
                return false;
        }

        return true;
    }

    public void SetCarPosition(CarController car, Vector2Int oldOrigin, Vector2Int newOrigin)
    {
        foreach (var cell in car.GetOccupiedCells(oldOrigin))
        {
            if (IsInsideBoard(cell))
            {
                grid[cell.x, cell.y] = null;
            }
        }

        foreach (var cell in car.GetOccupiedCells(newOrigin))
        {
            if (IsInsideBoard(cell))
            {
                grid[cell.x, cell.y] = car;
            }
        }
    }

    void SpawnExit()
    {
        Vector3 position = new Vector3(
            boardWidth * tileSpacing + tileSpacing / 2f,
            0.1f,
            3 * tileSpacing + tileSpacing / 2f
        );

        Instantiate(exitPrefab, position, Quaternion.identity);
    }

    public bool IsExitCell(Vector2Int cell, CarController car)
    {
        return car.isHorizontal &&
            car.isMainCar &&
            cell.x == boardWidth;
    }

    public void CheckWin(CarController car)
    {
        if (!car.isMainCar)
            return;

        
        foreach (var cell in car.GetOccupiedCells(car.gridPosition))
        {
            if (IsExitCell(cell, car))
            {
                Win();
                return;
            }
        }
    }

    void Win()
    {
        if (gameWon)
            return;

        gameWon = true;

        Debug.Log("PUZZLE COMPLETE!");

        AudioManager audioManager = AudioManager.Instance;
        if (audioManager != null)
        {
            audioManager.PlayWin();
        }

        if (winText != null)
            winText.SetActive(true);
    }

    public void ResetPuzzle()
    {
        gameWon = false;

        AudioManager audioManager = AudioManager.Instance;
        if (audioManager != null)
        {
            audioManager.PlayReset();
            audioManager.PlayMusicLoop();
        }

        if (winText != null)
            winText.SetActive(false);

        grid = new CarController[boardWidth, boardHeight];

        foreach (var pair in startingPositions)
        {
            CarController car = pair.Key;
            Vector2Int startPos = pair.Value;

            car.gridPosition = startPos;
            car.transform.position = GridToWorld(
                startPos,
                car.isHorizontal,
                car.length
            );

            foreach (var cell in car.GetOccupiedCells(startPos))
            {
                if (IsInsideBoard(cell))
                {
                    grid[cell.x, cell.y] = car;
                }
            }
        }
    }
}
