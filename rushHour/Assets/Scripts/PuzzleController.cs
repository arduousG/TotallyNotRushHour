using UnityEngine;

public class PuzzleController : MonoBehaviour
{
    public GameObject boardTilePrefab;
    public GameObject carPrefab;

    public int boardWidth = 6;
    public int boardHeight = 6;

    public float tileSpacing = 5f;

    void Start()
    {
        GenerateBoard();
        spawnCar();
    }

    void GenerateBoard()
    {
        for (int x = 0; x < boardWidth; x++)
        {
            for (int y = 0; y < boardHeight; y++)
            {
                Vector3 position = new Vector3(
                    x * tileSpacing,
                    0,
                    y * tileSpacing
                );

                Instantiate(boardTilePrefab, position, Quaternion.identity);
            }
        }
    }

    void spawnCar()
    {
        Vector3 position = new Vector3(
            0, 0, 0
        );
        Instantiate(carPrefab, position, Quaternion.identity);
    }
}
