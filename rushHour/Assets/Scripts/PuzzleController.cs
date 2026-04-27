using UnityEngine;

public class PuzzleController : MonoBehaviour
{
    public GameObject boardTilePrefab;
    public GameObject carPrefab;

    public CarSpawnData[] cars;

    public int boardWidth = 6;
    public int boardHeight = 6;

    public float tileSpacing = 5f;

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
        SpawnCars();
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
        }
    }
}
