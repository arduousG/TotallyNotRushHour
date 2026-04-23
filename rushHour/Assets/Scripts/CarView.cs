using UnityEngine;

// this script is responsible for setting up the car's visual appearance based on its properties (length, orientation, main car status) and applying the appropriate color. It uses a MaterialPropertyBlock to efficiently change the color without creating new material instances, ensuring good performance even with many cars in the scene.
// usage (for spawning cars):
// CarView view = spawnedCar.GetComponent<CarView>();
// view.Initialize(id, isMainCar, length, isHorizontal);

public class CarView : MonoBehaviour
{
    [Header("Car Info")]
    public int carId;
    public bool isMainCar;
    public int length = 2; // 2 = car, 3 = truck
    public bool isHorizontal = true;

    [Header("Grid Settings")]
    public float cellSize = 5f;
    public float carHeight = 5f;

    [Header("Colors")]
    public Color mainCarColor = Color.red;

    private Renderer carRenderer;
    private MaterialPropertyBlock propertyBlock;

    private static readonly Color[] randomColors =
    {
        Color.blue,
        Color.green,
        Color.yellow,
        Color.cyan,
        new Color(1f, 0.5f, 0f),   // orange
        new Color(0.6f, 0f, 1f),   // purple
        new Color(1f, 0.4f, 0.7f), // pink
        Color.white,
        Color.gray
    };

    private void Awake()
    {
        carRenderer = GetComponent<Renderer>();
        propertyBlock = new MaterialPropertyBlock();
    }

    public void Initialize(int id, bool mainCar, int carLength, bool horizontal)
    {
        carId = id;
        isMainCar = mainCar;
        length = carLength;
        isHorizontal = horizontal;

        ApplyScale();
        ApplyColor();
    }

    private void ApplyScale()
    {
        if (isHorizontal)
        {
            transform.localScale = new Vector3(length * cellSize, carHeight, cellSize);
        }
        else
        {
            transform.localScale = new Vector3(cellSize, carHeight, length * cellSize);
        }
    }

    private void ApplyColor()
    {
        Color chosenColor = isMainCar ? mainCarColor : GetRandomNonRedColor();
        SetColor(chosenColor);
    }

    private Color GetRandomNonRedColor()
    {
        return randomColors[Random.Range(0, randomColors.Length)];
    }

    private void SetColor(Color color)
    {
        if (carRenderer == null)
        {
            carRenderer = GetComponent<Renderer>();
        }

        carRenderer.GetPropertyBlock(propertyBlock);

        // Built-in/Standard shader
        propertyBlock.SetColor("_Color", color);

        // URP Lit shader
        propertyBlock.SetColor("_BaseColor", color);

        carRenderer.SetPropertyBlock(propertyBlock);
    }
}
