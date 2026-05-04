using UnityEngine;
using System.Collections.Generic;

public class PuzzleController : MonoBehaviour
{
    public enum Diff
    {
        Beginner,
        Intermediate,
        Advanced,
        Expert
    }

    public GameObject boardTilePrefab;
    public GameObject carPrefab;
    public GameObject exitPrefab;
    public GameObject winText;

    public CarSpawnData[] cars;

    public int boardWidth = 6;
    public int boardHeight = 6;

    public float tileSpacing = 5f;

    [Header("Core Rules")]
    public int exitRow = 3;

    [Header("Dynamic Level Settings")]
    public bool useDynLvls = true;
    public Diff activeDiff = Diff.Beginner;
    public int activeLvlIdx = 0;

    private bool gameWon = false;
    public bool IsGameWon => gameWon;

    private CarController[,] grid;
    private Dictionary<CarController, Vector2Int> startingPositions = 
        new Dictionary<CarController, Vector2Int>();
    // keep refs to spawned cars so level swap can clear clean
    private List<CarController> liveCars = new List<CarController>();
    // runtime color map (carId -> live color) -- for overlay reads
    private Dictionary<int, Color32> carColorById = new Dictionary<int, Color32>();
    private Dictionary<Diff, List<string>> lvlDb = new Dictionary<Diff, List<string>>();
    private string currentBoardString = "";
    private string currentLevelId = "";
    private int currentSourceScore = -1;

    public string CurrentBoardString
    {
        get { return currentBoardString; }
    }

    public string CurrentLevelId
    {
        get { return currentLevelId; }
    }

    public int CurrentSourceScore
    {
        get { return currentSourceScore; }
    }

    public bool GetCarColorById(int carId, out Color32 color) //solution overlay will know colors for grid -- exposing this method rather than just dict
    {
        if (carColorById.ContainsKey(carId))
        {
            color = carColorById[carId];
            return true;
        }

        color = default(Color32);
        return false;
    }

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
        SpawnExit();

        InitLvlDb();
        LoadActiveLvl();

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

        // debug keys for test difficulty - b4 ADD: menu UI (level select)- 1: beginner, 2: intermediate, 3: advanced, 4: expert
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            SetDiff(Diff.Beginner);
        }
        if (Input.GetKeyDown(KeyCode.Alpha2)) 
        {
            SetDiff(Diff.Intermediate);
        }
        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            SetDiff(Diff.Advanced);
        }
        if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            SetDiff(Diff.Expert);
        }
    }

    void InitLvlDb()
    {
        lvlDb = new Dictionary<Diff, List<string>>();

        // Curated set: one row per difficulty, format is "score board id"
        // src file: rush1000.txt from https://www.michaelfogleman.com/static/rush/rush1000.txt
        // hardcoded for alpha, will add UI for dynamic loading later if time allows
        lvlDb[Diff.Beginner] = new List<string>
        {
            "38 BBKCCoJoKoMxJoAAMoEEoLxNGGoLoNHHIIIo 6061"
        };

        lvlDb[Diff.Intermediate] = new List<string>
        {
            "41 xCCJoooooJLMAAoKLMHDDKoNHoIEENFFIGGG 3519"
        };

        lvlDb[Diff.Advanced] = new List<string>
        {
            "38 FBBBoKFoGHoKAAGHJKCCCIJooooIDDooxooo 2588"
        };

        lvlDb[Diff.Expert] = new List<string>
        {
            "43 HoBBBMHCCJLMAAIJLoDDIKLooooKEEoFFGGo 16930"
        };
    }

    public void SetDiffBeginner()
    {
        SetDiff(Diff.Beginner);
    }
    public void SetDiffIntermediate()
    {
        SetDiff(Diff.Intermediate);
    }
    public void SetDiffAdvanced()
    {
        SetDiff(Diff.Advanced);
    }
    public void SetDiffExpert()
    {
        SetDiff(Diff.Expert);
    }
    public void SetDiff(Diff diff)
    {
        activeDiff = diff;
        activeLvlIdx = 0;
        LoadActiveLvl();
    }
    public void NextLvl()
    {
        if (!lvlDb.ContainsKey(activeDiff))
        {
            return;
        }

        List<string> lvls = lvlDb[activeDiff];
        if (lvls.Count == 0)
        {
            return;
        }

        activeLvlIdx = (activeLvlIdx + 1) % lvls.Count;
        LoadActiveLvl();
    }

    void LoadActiveLvl()
    {
        gameWon = false;
        if (winText != null)
        {
            winText.SetActive(false);
        }

        //new level: clear old spawned cars + selected state first
        ClearLiveCars();
        CarController.ClearSel();

        if (useDynLvls)
        {
            CarSpawnData[] parsedCars = GetCarsFromDb(activeDiff, activeLvlIdx);
            if (parsedCars != null && parsedCars.Length > 0)
            {
                if (IsValidLvl(parsedCars))
                {
                    cars = parsedCars;
                }
                else
                {
                    Debug.LogWarning("level parsed not valid. Keeping prev array");
                }
            }
        }

        EnforceMainCarExitRow();

        if (!IsValidLvl(cars))
        {
            Debug.LogWarning("Current level data invalid after enforcement.");
            return;
        }

        //reset runtime maps per active level instance
        grid = new CarController[boardWidth, boardHeight];
        carColorById = new Dictionary<int, Color32>();
        startingPositions = new Dictionary<CarController, Vector2Int>();
        SpawnCars();
    }

    bool IsValidLvl(CarSpawnData[] inCars)
    {
        if (inCars == null || inCars.Length == 0)
        {
            return false;
        }

        bool foundMain = false;
        HashSet<Vector2Int> used = new HashSet<Vector2Int>();

        for (int i = 0; i < inCars.Length; i++)
        {
            CarSpawnData car = inCars[i];

            if (car.isMainCar)
            {
                foundMain = true;
            }

            int len = car.length;
            if (len < 2 || len > 3)
            {
                Debug.LogWarning("Invalid car length in level: " + len);
                return false;
            }

            for (int s = 0; s < len; s++)
            {
                int x = car.gridPosition.x;
                int y = car.gridPosition.y;

                if (car.isHorizontal)
                {
                    x = x + s;
                }
                else
                {
                    y = y + s;
                }

                Vector2Int cell = new Vector2Int(x, y);

                if (!IsInsideBoard(cell))
                {
                    Debug.LogWarning("Out of bounds car cell: " + cell);
                    return false;
                }
                if (used.Contains(cell))
                {
                    Debug.LogWarning("Overlapping car cell: " + cell);
                    return false;
                }
                used.Add(cell);
            }
        }
        if (!foundMain)
        {
            Debug.LogWarning("Level has no main car");
            return false;
        }
        return true;
    }

    void EnforceMainCarExitRow()
    {
        if (cars == null)
        {
            return;
        }

        bool hasMainCar = false;
        int clampedExitRow = Mathf.Clamp(exitRow, 0, boardHeight - 1);

        for (int i = 0; i < cars.Length; i++)
        {
            if (!cars[i].isMainCar)
            {
                continue;
            }

            hasMainCar = true;
            cars[i].isHorizontal = true;
            cars[i].gridPosition = new Vector2Int(cars[i].gridPosition.x, clampedExitRow);
        }
        if (!hasMainCar)
        {
            Debug.LogWarning("main car not found in level data");
        }
    }

    void ClearLiveCars()
    {
        foreach (CarController car in liveCars)
        {
            if (car != null)
            {
                Destroy(car.gameObject);
            }
        }
        liveCars.Clear();
    }

    CarSpawnData[] GetCarsFromDb(Diff diff, int lvlIdx)
    {
        if (!lvlDb.ContainsKey(diff))
        {
            return null;
        }

        List<string> lvls = lvlDb[diff];
        if (lvls.Count == 0)
        {
            return null;
        }
        if (lvlIdx < 0)
        {
            lvlIdx = 0;
        }
        if (lvlIdx >= lvls.Count)
        {
            lvlIdx = lvls.Count - 1;
        }
        return ParseLvl(lvls[lvlIdx]);
    }

    CarSpawnData[] ParseLvl(string raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return null;
        }

        string[] parts = raw.Split(new char[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3)
        {
            Debug.LogWarning("Invalid level row: " + raw);
            return null;
        }

        int parsedScore = -1;
        int.TryParse(parts[0], out parsedScore);

        currentSourceScore = parsedScore; //what overlay is reading for score, L45
        // overlay matching keys come from these 2 vals
        currentBoardString = parts[1].Trim();
        currentLevelId = parts[2].Trim();

        string board = parts[1].Trim();
        return ParseBoard(board);
    }

    CarSpawnData[] ParseBoard(string board)
    {
        int cellCount = boardWidth * boardHeight;
        if (string.IsNullOrEmpty(board) || board.Length != cellCount)
        {
            Debug.LogWarning("Invalid board string length: " + board);
            return null;
        }

        Dictionary<char, List<Vector2Int>> map = new Dictionary<char, List<Vector2Int>>();

        for (int i = 0; i < board.Length; i++)
        {
            char ch = board[i];
            if (ch == 'o' || ch == 'x')
            {
                continue;
            }

            int rowTopToBottom = i / boardWidth;
            int col = i % boardWidth;
            int x = col;
            //input board is top-down -- grid system is bottom-up
            int y = (boardHeight - 1) - rowTopToBottom;
            Vector2Int pos = new Vector2Int(x, y);

            if (!map.ContainsKey(ch))
            {
                map[ch] = new List<Vector2Int>();
            }
            map[ch].Add(pos);
        }

        List<CarSpawnData> outCars = new List<CarSpawnData>();
        int carId = 0;
        for (char ch = 'A'; ch <= 'Z'; ch++)
        {
            if (!map.ContainsKey(ch))
            {
                continue;
            }

            List<Vector2Int> cells = map[ch];
            if (cells.Count < 2)
            {
                Debug.LogWarning("Skipping invalid car (len < 2): " + ch);
                continue;
            }
            int minX = cells[0].x;
            int maxX = cells[0].x;
            int minY = cells[0].y;
            int maxY = cells[0].y;

            for (int i = 1; i < cells.Count; i++)
            {
                if (cells[i].x < minX) minX = cells[i].x;
                if (cells[i].x > maxX) maxX = cells[i].x;
                if (cells[i].y < minY) minY = cells[i].y;
                if (cells[i].y > maxY) maxY = cells[i].y;
            }

            bool isHoriz = minY == maxY;
            int len = cells.Count;
            CarSpawnData car = new CarSpawnData();
            car.carId = ch - 'A';
            car.isMainCar = ch == 'A';
            car.isHorizontal = isHoriz;
            car.length = len;
            car.gridPosition = new Vector2Int(minX, minY);
            outCars.Add(car);
            carId++;
        }
        return outCars.ToArray();
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

                // capture chosen runtime color so overlay mirrors exact car tint
                carColorById[car.carId] = (Color32)view.CurrentColor;
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
                // tracked for full cleanup when loading next difficulty/level
                liveCars.Add(controller);

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
                {
                    return false;
                }

                continue;
            }

            CarController occupyingCar = grid[cell.x, cell.y];
            if (occupyingCar != null && occupyingCar != car)
            {
                return false;
            }
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
        int clampedExitRow = Mathf.Clamp(exitRow, 0, boardHeight - 1);

        Vector3 position = new Vector3(
            boardWidth * tileSpacing + tileSpacing / 2f,
            0.1f,
            clampedExitRow * tileSpacing + tileSpacing / 2f
        );

        Instantiate(exitPrefab, position, Quaternion.identity);
    }

    public bool IsExitCell(Vector2Int cell, CarController car)
    {
        int clampedExitRow = Mathf.Clamp(exitRow, 0, boardHeight - 1);

        return car.isHorizontal &&
            car.isMainCar &&
            cell.x == boardWidth &&
            cell.y == clampedExitRow;
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
