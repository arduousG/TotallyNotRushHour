using System;
using UnityEngine;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;

public class PuzzleController : MonoBehaviour
{
    public event Action<Diff, int, bool> LevelCompletionChanged;
    public event Action<PuzzleCompletionResult> PuzzleCompleted;

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

    public EnvironmentManager environmentManager;

    [Header("Win VFX")]
    public ParticleSystem winConfetti;
    public ParticleSystem winConfettiPrefab;
    public Transform winConfettiSpawnPoint;

    public TMP_Text moveCounterText;

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
    public EndlessModeController endlessModeController;

    [Header("Valid Move Highlight")]
    //toggle legal move tile tinting/highlighting
    public bool showValidMoveHighlight = true;
    //tint/highlight applied to board tiles that belong to  >=one legal dest footprint
    public Color highlightColor = new Color(0.2f, 0.85f, 1f, 0.5f);

    [Header("Menu")]
    public GameObject mainMenuPanel;
    public GameObject gameplayUi;

    [Header("Settings and Help")]
    public GameObject settingsPanel;
    public Slider musicVolumeSlider;
    public Slider sfxVolumeSlider;
    public Slider uiVolumeSlider;
    public GameObject rulesControlsPanel;
    public TMP_Text rulesControlsText;
    [TextArea(5, 20)]
    public string defaultRulesControlsText =
        "Goal: Time to get out of a TotallyNotRushHour traffic jam! Maneuver the cars inside this traffic jam with the goal of getting the red car to the marked exit on the upper-middle right-hand side in as few moves as possible!\n" +
        "\n" +
        "Controls:\n" +
        "- Click a car to select it, after selected -> available moves are highlighted\n" +
        "- Click + drag selected car to move it\n" +
        "- Arrow keys can also move your selected car\n" +
        "- R resets the level (same as reset button)\n" +
        "- Tab toggles solution overlay, which shows step-by-step moves from the puzzle level start (fewest possible moves/best solution is shown)\n" +
        "- F1 toggles the rules panel\n" +
        "- F2 toggles the settings panel\n" +
        "\n"
        + "Enjoy!";
    [SerializeField] private KeyCode toggleRulesControlsKey = KeyCode.F1;
    [SerializeField] private KeyCode toggleSettingsKey = KeyCode.F2;

    [Header("Audio")]
    [SerializeField] private bool playMusicInMenu = true;

    private bool suppressVolumeSliderCallbacks = false;

    private bool gameWon = false;

    private bool isGameplayActive = false;
    private bool isEndlessMode = false;
    public bool IsGameWon => gameWon;
    private int moveCount = 0;
    public int MoveCount => moveCount;
    public bool IsEndlessMode => isEndlessMode;

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
    private int currentMinimumMoves = -1;
    private float levelStartTime;
    //board tile renderers indexed by board coordinates for direct tinting of grid for highlights
    private Renderer[,] boardTileRenderers;
    //tracks currently tinted tiles for qucik reset
    private readonly List<Vector2Int> highlightedCells = new List<Vector2Int>();
    //shared property block to avoid creating runtime material instances per cell
    private MaterialPropertyBlock tilePropertyBlock;
    //cache of last rendered selection state -> skip redundant highlight rebuild
    private CarController lastHighlightCar;
    private Vector2Int lastHighlightOrigin;
    private ParticleSystem runtimeWinConfetti;

    private bool boardGenerated = false;
    private readonly List<Vector2Int> occupiedCellBuffer = new List<Vector2Int>(3);
    private readonly List<Vector2Int> highlightOccupiedCellBuffer = new List<Vector2Int>(3);
    private GameObject exitObject;
    private GameObject[,] boardTiles;
    private const string CompletionPrefKeyPrefix = "rushhour.level.completed";

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

    public int CurrentMinimumMoves
    {
        get { return currentMinimumMoves; }
    }

    public float CurrentElapsedTime
    {
        get { return Mathf.Max(0f, Time.time - levelStartTime); }
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

    public class PuzzleCompletionResult
    {
        public Diff diff;
        public int levelIndex;
        public string levelId;
        public string board;
        public int sourceScore;
        public int minimumMoves;
        public int movesUsed;
        public float elapsedSeconds;
        public bool endlessMode;
    }

    //init reusable property block @ runtime for tile highlight updates
    void Awake()
    {
        tilePropertyBlock = new MaterialPropertyBlock();
    }

    void Start()
    {
        InitLvlDb();

        mainMenuPanel.SetActive(true);
        gameplayUi.SetActive(false);
        InitializeUiPanels();
        InitializeAudioSettingsUi();
        EnsureEndlessModeController();

        AudioManager audioManager = AudioManager.Instance;
        if (audioManager != null)
        {
            if (playMusicInMenu)
            {
                audioManager.PlayMenuMusicLoopWithFade();
            }
            else
            {
                audioManager.StopMusic();
            }
        }
    }

    public void StartGame()
    {
        PlayUiClick();
        isEndlessMode = false;

        mainMenuPanel.SetActive(false);
        gameplayUi.SetActive(true);
        isGameplayActive = true;

        SetPanelActive(settingsPanel, false);
        SetPanelActive(rulesControlsPanel, false);

        AudioManager audioManager = AudioManager.Instance;
        if (audioManager != null)
        {
            audioManager.PlayGameplayMusicLoopWithFade();
        }

        if (!boardGenerated)
        {
            GenerateBoard();
            SpawnExit();
            boardGenerated = true;
        }
        else
        {
            foreach(GameObject tile in boardTiles)
            {
                if(tile != null)
                {
                    tile.SetActive(true);
                }
            }

            if(exitObject != null)
            {
                exitObject.SetActive(true);
            }
        }
        LoadActiveLvl();
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleSettingsKey))
        {
            ToggleSettingsPanel();
        }

        if (Input.GetKeyDown(toggleRulesControlsKey))
        {
            ToggleRulesControlsPanel();
        }

        if (isGameplayActive && Input.GetKeyDown(KeyCode.R))
        {
            ResetPuzzle();
        }

        // debug keys for test difficulty - b4 ADD: menu UI (level select)- 1: beginner, 2: intermediate, 3: advanced, 4: expert
        if (isGameplayActive)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                SetDiff(Diff.Beginner);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                SetDiff(Diff.Intermediate);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                SetDiff(Diff.Advanced);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha4))
            {
                SetDiff(Diff.Expert);
            }
        }
        
        if (isGameplayActive)
        {
            UpdateMoveHighlight();
        }
    }

    void InitLvlDb()
    {
        lvlDb = new Dictionary<Diff, List<string>>();

        // beta set: 5 levels per difficulty, format "score board id"
        lvlDb[Diff.Beginner] = new List<string>
        {
            "39 JBBxDDJoLEENAALooNKFFMooKooMGGHHHxoo 1016",
            "42 BBCCoxKooMEEKAAMoNKFFGGNooLoHHIILJJJ 1365",
            "44 ooIBBMCCIJoMHAAJLoHoDDLxFFFKLoGGGKoo 2137",
            "42 ooIBBMCCIJLMHAAJLoHoDDxoFFFKooGGoKoo 6190",
            "40 ooJBBMCCJKoMIAAKooIDDLEEFFFLoooxHHHo 730"
        };

        lvlDb[Diff.Intermediate] = new List<string>
        {
            "50 ooooxoCCCJLoAAIJLMooIDDMHEEKooHFFKox 3874",
            "40 ooIBBxooIJLoAAIJLoDDoKEEHFFKoMHGGGoM 2273",
            "38 BBBKxoooJKDDAAJKoMIEEFFMIoGGLoHHooLo 9542",
            "43 GBBJKoGoHJKxAAHJoLDDEEoLooIoooooIFFF 860",
            "39 oooxLoHCCKLoHAAKLoDDJooooIJEEooIFFGG 8016"
        };

        lvlDb[Diff.Advanced] = new List<string>
        {
            "41 BBCCCoIooKDDIAAKoLooJEELFFJooMGGJHHM 2882",
            "41 BBHCCoGoHoJoGAAoJoGDDDJooooIooEEoIFF 594",
            "38 oBBBCCoDDxoMAAJooMIoJFFNIGGKLNHHHKLN 1544",
            "42 BBICCoHoIoKoHAAoKoHDDDKooooJEEFFoJGG 1090",
            "38 xoCCCMDDJoLMAAJoLNoIEELNHIoKFFHGGKoo 24250"
        };

        lvlDb[Diff.Expert] = new List<string>
        {
            "40 FoooooFBBBJoAAGHJoCCGHJooooIDDoEEIoo 1680",
            "39 oooKBBHIoKLMHIAALMHCCDDMooJEEoFFJGGo 7598",
            "39 BBBJoooooJLMAAoKLMHCCKxNHoIEENFFIoxo 6377",
            "38 oooIBBooHICCAAHJKMDDoJKMGEEELNGFFFLN 4687",
            "39 oBBBKMCCoIKMAAoILoGDDJLoGoHJEEFFHooo 28276"
        };
    }

    public void SetDiffBeginner()
    {
        if (!isGameplayActive)
        {
            return;
        }

        SetDiff(Diff.Beginner);
    }
    public void SetDiffIntermediate()
    {
        if (!isGameplayActive)
        {
            return;
        }

        SetDiff(Diff.Intermediate);
    }
    public void SetDiffAdvanced()
    {
        if (!isGameplayActive)
        {
            return;
        }

        SetDiff(Diff.Advanced);
    }
    public void SetDiffExpert()
    {
        if (!isGameplayActive)
        {
            return;
        }

        SetDiff(Diff.Expert);
    }

    void StartGameWithDifficulty(Diff diff)
    {
        StartGameAtLevel(diff, 0);
    }

    public void StartGameAtLevel(Diff diff, int levelIndex)
    {
        isEndlessMode = false;
        activeDiff = diff;

        AudioManager audioManager = AudioManager.Instance;
        if (audioManager != null)
        {
            audioManager.PlayGameplayMusicLoopWithFade();
        }

        List<string> levelsForDiff;
        if (!lvlDb.TryGetValue(diff, out levelsForDiff) || levelsForDiff.Count == 0)
        {
            activeLvlIdx = 0;
            return;
        }

        activeLvlIdx = Mathf.Clamp(levelIndex, 0, levelsForDiff.Count - 1);

        // Hide menu and show game UI
        mainMenuPanel.SetActive(false);
        gameplayUi.SetActive(true);
        isGameplayActive = true;

        // Create board first time, otherwise reactivate it
        if (!boardGenerated)
        {
            GenerateBoard();
            SpawnExit();
            boardGenerated = true;
        }
        else
        {
            foreach (GameObject tile in boardTiles)
            {
                if (tile != null)
                {
                    tile.SetActive(true);
                }
            }

            if (exitObject != null)
            {
                exitObject.SetActive(true);
            }
        }

        if (environmentManager != null)
        {
            environmentManager.LoadEnvironment(diff);
        }

        LoadActiveLvl();
    }

    public void StartEndlessGame()
    {
        PlayUiClick();
        isEndlessMode = true;

        mainMenuPanel.SetActive(false);
        gameplayUi.SetActive(true);
        isGameplayActive = true;

        SetPanelActive(settingsPanel, false);
        SetPanelActive(rulesControlsPanel, false);

        AudioManager audioManager = AudioManager.Instance;
        if (audioManager != null)
        {
            audioManager.PlayGameplayMusicLoopWithFade();
        }

        EnsureBoardVisible();
    }

    public void StartEndlessMode()
    {
        EnsureEndlessModeController();

        if (endlessModeController != null)
        {
            endlessModeController.StartEndlessMode();
        }
    }

    public void SetDiff(Diff diff)
    {
        isEndlessMode = false;
        activeDiff = diff;
        activeLvlIdx = 0;

        if (!isGameplayActive)
        {
            return;
        }

        LoadActiveLvl();
    }
    public void NextLvl()
    {
        isEndlessMode = false;

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
        PrepareForLevelLoad();

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

        FinishLevelLoad();
    }

    public bool LoadEndlessLevel(string board, string levelId, int minimumMoves, int sourceScore, Diff diff, int endlessLevelIndex)
    {
        if (string.IsNullOrEmpty(board))
        {
            return false;
        }

        isEndlessMode = true;
        activeDiff = diff;
        activeLvlIdx = endlessLevelIndex;
        currentBoardString = board.Trim();
        currentLevelId = levelId;
        currentMinimumMoves = minimumMoves;
        currentSourceScore = sourceScore;

        PrepareForLevelLoad();

        CarSpawnData[] parsedCars = ParseBoard(currentBoardString);
        if (parsedCars == null || parsedCars.Length == 0 || !IsValidLvl(parsedCars))
        {
            Debug.LogWarning("Endless level data invalid: " + levelId);
            return false;
        }

        cars = parsedCars;
        FinishLevelLoad();
        return true;
    }

    void PrepareForLevelLoad()
    {
        gameWon = false;
        StopWinConfetti();
        if (winText != null)
        {
            winText.SetActive(false);
        }

        moveCount = 0;
        UpdateMoveText();

        //new level, clean runtime state before rebuild: clear old spawned cars + selected state first
        ClearLiveCars();
        CarController.ClearSel();
        ClearMoveHighlights();
    }

    void FinishLevelLoad()
    {
        EnforceMainCarExitRow();

        if (!IsValidLvl(cars))
        {
            Debug.LogWarning("Current level data invalid after enforcement.");
            return;
        }

        // reset/rebuild runtime maps per active level instance ->  occupancy/color data match freshly spawned cars
        grid = new CarController[boardWidth, boardHeight];
        carColorById = new Dictionary<int, Color32>();
        startingPositions = new Dictionary<CarController, Vector2Int>();
        SpawnCars();
        UpdateMoveHighlight();
        levelStartTime = Time.time;
    }

    bool IsValidLvl(CarSpawnData[] inCars)
    {
        if (inCars == null || inCars.Length == 0)
        {
            return false;
        }

        bool foundMain = false;
        //occupied cell tracking whilst validating to reject overlaps + detect out of bounds cars
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
        CarController.ClearSel();

        foreach (CarController car in liveCars)
        {
            if (car != null)
            {
                Destroy(car.gameObject);
            }
        }

        liveCars.Clear();
        startingPositions.Clear();
        carColorById.Clear();
        grid = null;
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
        currentMinimumMoves = -1;
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

        //group board cells by piece letter --> each car can be reconstructed
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
        boardTileRenderers = new Renderer[boardWidth, boardHeight];
        boardTiles = new GameObject[boardWidth, boardHeight];

        for (int x = 0; x < boardWidth; x++)
        {
            for (int y = 0; y < boardHeight; y++)
            {
                Vector3 position = new Vector3(
                    x * tileSpacing + tileSpacing / 2f,
                    0,
                    y * tileSpacing + tileSpacing / 2f
                );

                GameObject tile = Instantiate(
                    boardTilePrefab,
                    position,
                    Quaternion.identity
                );

                boardTiles[x,y] = tile;

                Renderer tileRenderer =
                    tile.GetComponentInChildren<Renderer>();

                boardTileRenderers[x,y] = tileRenderer;
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

                controller.GetOccupiedCells(car.gridPosition, occupiedCellBuffer);
                for (int i = 0; i < occupiedCellBuffer.Count; i++)
                {
                    Vector2Int cell = occupiedCellBuffer[i];
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
        car.GetOccupiedCells(newOrigin, occupiedCellBuffer);

        for (int i = 0; i < occupiedCellBuffer.Count; i++)
        {
            Vector2Int cell = occupiedCellBuffer[i];

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
        car.GetOccupiedCells(oldOrigin, occupiedCellBuffer);
        for (int i = 0; i < occupiedCellBuffer.Count; i++)
        {
            Vector2Int cell = occupiedCellBuffer[i];
            if (IsInsideBoard(cell))
            {
                grid[cell.x, cell.y] = null;
            }
        }

        car.GetOccupiedCells(newOrigin, occupiedCellBuffer);
        for (int i = 0; i < occupiedCellBuffer.Count; i++)
        {
            Vector2Int cell = occupiedCellBuffer[i];
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

        exitObject = Instantiate(
            exitPrefab,
            position,
            Quaternion.identity
        );

        ExitMarkerGlow exitGlow = exitObject.GetComponent<ExitMarkerGlow>();
        if (exitGlow == null)
        {
            exitGlow = exitObject.AddComponent<ExitMarkerGlow>();
        }
        exitGlow.ApplyGlow();
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

        
        car.GetOccupiedCells(car.gridPosition, occupiedCellBuffer);
        for (int i = 0; i < occupiedCellBuffer.Count; i++)
        {
            if (IsExitCell(occupiedCellBuffer[i], car))
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
        if (!isEndlessMode)
        {
            MarkCurrentLevelCompleted();
        }

        Debug.Log("PUZZLE COMPLETE IN " + moveCount + " MOVES!");

        AudioManager audioManager = AudioManager.Instance;
        if (audioManager != null)
        {
            audioManager.FadeMusicTo(0.15f, 0.2f);
            audioManager.PlayWin();
        }

        if (winText != null)
            winText.SetActive(true);

        PlayWinConfetti();

        Action<PuzzleCompletionResult> callback = PuzzleCompleted;
        if (callback != null)
        {
            PuzzleCompletionResult result = new PuzzleCompletionResult();
            result.diff = activeDiff;
            result.levelIndex = activeLvlIdx;
            result.levelId = currentLevelId;
            result.board = currentBoardString;
            result.sourceScore = currentSourceScore;
            result.minimumMoves = currentMinimumMoves;
            result.movesUsed = moveCount;
            result.elapsedSeconds = CurrentElapsedTime;
            result.endlessMode = isEndlessMode;
            callback(result);
        }
    }

    public void ResetPuzzle()
    {
        if (!isGameplayActive || startingPositions.Count == 0)
        {
            return;
        }
        
        gameWon = false;
        StopWinConfetti();
        moveCount = 0;
        UpdateMoveText();

        AudioManager audioManager = AudioManager.Instance;
        if (audioManager != null)
        {
            audioManager.PlayReset();
            audioManager.PlayGameplayMusicLoopWithFade();
        }

        if (winText != null)
            winText.SetActive(false);

        grid = new CarController[boardWidth, boardHeight];

        foreach (var pair in startingPositions)
        {
            CarController car = pair.Key;
            if (car == null)
            {    
                continue;
            }

            Vector2Int startPos = pair.Value;

            car.gridPosition = startPos;
            car.transform.position = GridToWorld(
                startPos,
                car.isHorizontal,
                car.length
            );

            car.GetOccupiedCells(startPos, occupiedCellBuffer);
            for (int i = 0; i < occupiedCellBuffer.Count; i++)
            {
                Vector2Int cell = occupiedCellBuffer[i];
                if (IsInsideBoard(cell))
                {
                    grid[cell.x, cell.y] = car;
                }
            }
        }
        UpdateMoveHighlight(forceRefresh: true);
    }

    void UpdateMoveHighlight(bool forceRefresh = false)
    {
        if (!showValidMoveHighlight || gameWon)
        {
            ClearMoveHighlights();
            lastHighlightCar = null;
            return;
        }
        CarController selected = CarController.CurrentSelected;
        if (selected == null || selected.puzzle != this)
        {
            ClearMoveHighlights();
            lastHighlightCar = null;
            return;
        }
        //skip when selected car and origin have not changed
        if (!forceRefresh && selected == lastHighlightCar && selected.gridPosition == lastHighlightOrigin)
        {
            return;
        }
        //rebuild markers only when selected car or origin have changed
        ClearMoveHighlights();
        List<Vector2Int> validOrigins = GetValidOriginsForCar(selected);

        //merge all dest footprints into one unique set of cells to highlight. -> apply in one pass
        HashSet<Vector2Int> cellsToHighlight = new HashSet<Vector2Int>();
        for (int i = 0; i < validOrigins.Count; i++)
        {
            selected.GetOccupiedCells(validOrigins[i], highlightOccupiedCellBuffer);
            for (int c = 0; c < highlightOccupiedCellBuffer.Count; c++)
            {
                Vector2Int occupied = highlightOccupiedCellBuffer[c];
                if (IsInsideBoard(occupied))
                {
                    cellsToHighlight.Add(occupied);
                }
            }
        }
        ApplyBoardHighlights(cellsToHighlight);
        lastHighlightCar = selected;
        lastHighlightOrigin = selected.gridPosition;
    }

    List<Vector2Int> GetValidOriginsForCar(CarController car)
    {
        List<Vector2Int> origins = new List<Vector2Int>();
        if (car == null)
        {
            return origins;
        }
        Vector2Int[] directions;
        if (car.isHorizontal)
        {
            directions = new Vector2Int[] { Vector2Int.left, Vector2Int.right };
        }
        else
        {
            directions = new Vector2Int[] { Vector2Int.down, Vector2Int.up };
        }
        //step by step raycast along allowed axis until blocked to find every valid stop along the lane
        for (int d = 0; d < directions.Length; d++)
        {
            Vector2Int probe = car.gridPosition;
            while (true)
            {
                probe = probe + directions[d];
                if (!CanPlaceCar(car, probe))
                {
                    break;
                }
                //every reachable stop along ==> valid highlight target
                origins.Add(probe);
            }
        }
        return origins;
    }

    void ApplyBoardHighlights(HashSet<Vector2Int> cells)
    {
        if (cells == null || cells.Count == 0)
        {
            return;
        }
        foreach (Vector2Int cell in cells)
        {
            // Tint each target tile and remember it for fast revert.
            TintBoardCell(cell, highlightColor);
            highlightedCells.Add(cell);
        }
    }

    void TintBoardCell(Vector2Int cell, Color tint)
    {
        if (!IsInsideBoard(cell) || boardTileRenderers == null)
        {
            return;
        }
        Renderer tileRenderer = boardTileRenderers[cell.x, cell.y];
        if (tileRenderer == null)
        {
            return;
        }
        tilePropertyBlock.Clear();
        tileRenderer.GetPropertyBlock(tilePropertyBlock);

        // Support both URP and Built-in color property names.
        if (tileRenderer.sharedMaterial != null && tileRenderer.sharedMaterial.HasProperty("_BaseColor"))
        {
            tilePropertyBlock.SetColor("_BaseColor", tint);
        }
        if (tileRenderer.sharedMaterial != null && tileRenderer.sharedMaterial.HasProperty("_Color"))
        {
            tilePropertyBlock.SetColor("_Color", tint);
        }
        tileRenderer.SetPropertyBlock(tilePropertyBlock);
    }

    void ClearMoveHighlights()
    {
        for (int i = 0; i < highlightedCells.Count; i++)
        {
            Vector2Int cell = highlightedCells[i];
            if (!IsInsideBoard(cell) || boardTileRenderers == null)
            {
                continue;
            }

            Renderer tileRenderer = boardTileRenderers[cell.x, cell.y];
            if (tileRenderer == null)
            {
                continue;
            }
            tilePropertyBlock.Clear();
            // Empty property block restores the renderer's original material color.
            tileRenderer.SetPropertyBlock(tilePropertyBlock);
        }
        highlightedCells.Clear();
    }

    void OnDisable()
    {
        ClearMoveHighlights();
        StopWinConfetti();
    }

    public void RegisterMove()
    {
        moveCount++;
        UpdateMoveText();
    }

    void UpdateMoveText()
    {
        if (moveCounterText != null)
        {
            moveCounterText.text = "Moves: " + moveCount;
        }
    }

    public void ReturnToMenu()
    {
        PlayUiClick();

        isGameplayActive = false;
        isEndlessMode = false;

        AudioManager audioManager = AudioManager.Instance;
        if (audioManager != null)
        {
            if (playMusicInMenu)
            {
                audioManager.PlayMenuMusicLoopWithFade();
            }
            else
            {
                audioManager.StopMusicWithFade();
            }
        }
        
        mainMenuPanel.SetActive(true);
        gameplayUi.SetActive(false);
        SetPanelActive(settingsPanel, false);

        ClearMoveHighlights();
        ClearLiveCars();
        CarController.ClearSel(); // defensive may not be necessary

        gameWon = false;

        StopWinConfetti();

        if(boardTiles != null)
        {
            foreach(GameObject tile in boardTiles)
            {
                if(tile != null)
                {
                    tile.SetActive(false);
                }
            }
        }

        if(exitObject != null)
        {
            exitObject.SetActive(false);
        }

        if(environmentManager!=null)
        {
            environmentManager.HideEnvironment();
        }
    }

    void InitializeUiPanels()
    {
        SetPanelActive(settingsPanel, false);
        SetPanelActive(rulesControlsPanel, false);

        if (rulesControlsText != null && string.IsNullOrWhiteSpace(rulesControlsText.text))
        {
            rulesControlsText.text = defaultRulesControlsText;
        }
    }

    void InitializeAudioSettingsUi()
    {
        AudioManager audioManager = AudioManager.Instance;
        if (audioManager == null)
        {
            return;
        }

        suppressVolumeSliderCallbacks = true;

        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.value = audioManager.MusicVolume;
            musicVolumeSlider.onValueChanged.RemoveListener(OnMusicVolumeChanged);
            musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        }

        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.value = audioManager.SfxVolume;
            sfxVolumeSlider.onValueChanged.RemoveListener(OnSfxVolumeChanged);
            sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeChanged);
        }

        if (uiVolumeSlider != null)
        {
            uiVolumeSlider.value = audioManager.UiVolume;
            uiVolumeSlider.onValueChanged.RemoveListener(OnUiVolumeChanged);
            uiVolumeSlider.onValueChanged.AddListener(OnUiVolumeChanged);
        }

        suppressVolumeSliderCallbacks = false;
    }

    public void OpenSettingsPanel()
    {
        PlayUiClick();
        SetPanelActive(settingsPanel, true);
        InitializeAudioSettingsUi();
    }

    public void ToggleSettingsPanel()
    {
        if (settingsPanel == null)
        {
            return;
        }

        if (settingsPanel.activeSelf)
        {
            CloseSettingsPanel();
            return;
        }

        OpenSettingsPanel();
    }

    public void CloseSettingsPanel()
    {
        PlayUiClick();
        SetPanelActive(settingsPanel, false);
    }

    public void ToggleRulesControlsPanel()
    {
        if (rulesControlsPanel == null)
        {
            return;
        }

        bool show = !rulesControlsPanel.activeSelf;
        SetPanelActive(rulesControlsPanel, show);

        AudioManager audioManager = AudioManager.Instance;
        if (audioManager != null)
        {
            audioManager.PlayUIClick();
        }
    }

    public void OpenRulesControlsPanel()
    {
        PlayUiClick();
        SetPanelActive(rulesControlsPanel, true);
    }

    public void CloseRulesControlsPanel()
    {
        PlayUiClick();
        SetPanelActive(rulesControlsPanel, false);
    }

    public void OnMusicVolumeChanged(float value)
    {
        if (suppressVolumeSliderCallbacks)
        {
            return;
        }

        AudioManager audioManager = AudioManager.Instance;
        if (audioManager != null)
        {
            audioManager.SetMusicVolume(value);
        }
    }

    public void OnSfxVolumeChanged(float value)
    {
        if (suppressVolumeSliderCallbacks)
        {
            return;
        }

        AudioManager audioManager = AudioManager.Instance;
        if (audioManager != null)
        {
            audioManager.SetSfxVolume(value);
        }
    }

    public void OnUiVolumeChanged(float value)
    {
        if (suppressVolumeSliderCallbacks)
        {
            return;
        }

        AudioManager audioManager = AudioManager.Instance;
        if (audioManager != null)
        {
            audioManager.SetUiVolume(value);
            audioManager.PlayUIClick();
        }
    }

    void SetPanelActive(GameObject panel, bool active)
    {
        if (panel != null)
        {
            panel.SetActive(active);
        }
    }

    void PlayUiClick()
    {
        AudioManager audioManager = AudioManager.Instance;
        if (audioManager != null)
        {
            audioManager.PlayUIClick();
        }
    }

    string BuildCompletionPrefKey(Diff diff, int levelIndex)
    {
        return CompletionPrefKeyPrefix + "." + (int)diff + "." + levelIndex;
    }

    public bool IsLevelCompleted(Diff diff, int levelIndex)
    {
        return PlayerPrefs.GetInt(BuildCompletionPrefKey(diff, levelIndex), 0) == 1;
    }

    public bool IsCurrentLevelCompleted()
    {
        return IsLevelCompleted(activeDiff, activeLvlIdx);
    }

    public int GetLevelCount(Diff diff)
    {
        List<string> levels;
        if (!lvlDb.TryGetValue(diff, out levels) || levels == null)
        {
            return 0;
        }

        return levels.Count;
    }

    void EnsureBoardVisible()
    {
        if (!boardGenerated)
        {
            GenerateBoard();
            SpawnExit();
            boardGenerated = true;
            return;
        }

        foreach (GameObject tile in boardTiles)
        {
            if (tile != null)
            {
                tile.SetActive(true);
            }
        }

        if (exitObject != null)
        {
            exitObject.SetActive(true);
        }
    }

    void EnsureEndlessModeController()
    {
        if (endlessModeController == null)
        {
            endlessModeController = UnityEngine.Object.FindFirstObjectByType<EndlessModeController>();
        }

        if (endlessModeController == null)
        {
            endlessModeController = gameObject.AddComponent<EndlessModeController>();
        }

        endlessModeController.puzzle = this;

        if (endlessModeController.environmentManager == null)
        {
            endlessModeController.environmentManager = environmentManager;
        }
    }

    void MarkCurrentLevelCompleted()
    {
        string key = BuildCompletionPrefKey(activeDiff, activeLvlIdx);
        if (PlayerPrefs.GetInt(key, 0) == 1)
        {
            return;
        }

        PlayerPrefs.SetInt(key, 1);
        PlayerPrefs.Save();

        Action<Diff, int, bool> callback = LevelCompletionChanged;
        if (callback != null)
        {
            callback(activeDiff, activeLvlIdx, true);
        }
    }

    public void ClearAllCompletionProgress()
    {
        foreach (KeyValuePair<Diff, List<string>> pair in lvlDb)
        {
            int count = pair.Value != null ? pair.Value.Count : 0;
            for (int i = 0; i < count; i++)
            {
                PlayerPrefs.DeleteKey(BuildCompletionPrefKey(pair.Key, i));

                Action<Diff, int, bool> callback = LevelCompletionChanged;
                if (callback != null)
                {
                    callback(pair.Key, i, false);
                }
            }
        }

        PlayerPrefs.Save();
    }

    void PlayWinConfetti()
    {
        ParticleSystem confettiToPlay = null;

        //win Confetti :  either a scene instance or a prefab asset
        //if prefab asset -> instantiate once and reuse the runtime instance
        if (winConfetti != null)
        {
            if (winConfetti.gameObject.scene.IsValid())
            {
                confettiToPlay = winConfetti;
            }
            else
            {
                if (runtimeWinConfetti == null)
                {
                    runtimeWinConfetti = Instantiate(
                        winConfetti,
                        GetWinConfettiSpawnPosition(),
                        Quaternion.identity
                    );
                }
                else
                {
                    runtimeWinConfetti.transform.position = GetWinConfettiSpawnPosition();
                }

                confettiToPlay = runtimeWinConfetti;
            }
        }

        if (confettiToPlay == null && winConfettiPrefab != null)
        {
            if (runtimeWinConfetti == null)
            {
                runtimeWinConfetti = Instantiate(
                    winConfettiPrefab,
                    GetWinConfettiSpawnPosition(),
                    Quaternion.identity
                );
            }
            else
            {
                runtimeWinConfetti.transform.position = GetWinConfettiSpawnPosition();
            }

            confettiToPlay = runtimeWinConfetti;
        }

        if (confettiToPlay == null)
        {
            confettiToPlay = EnsureFallbackConfetti();
        }

        if (confettiToPlay == null)
        {
            Debug.LogWarning("Win confetti is not assigned and fallback confetti could not be created.");
            return;
        }

        if (!confettiToPlay.gameObject.activeSelf)
        {
            confettiToPlay.gameObject.SetActive(true);
        }

        confettiToPlay.Clear(true);
        confettiToPlay.Play(true);
    }

    ParticleSystem EnsureFallbackConfetti()
    {
        if (runtimeWinConfetti != null)
        {
            runtimeWinConfetti.transform.position = GetWinConfettiSpawnPosition();
            return runtimeWinConfetti;
        }

        GameObject fallbackObj = new GameObject("RuntimeWinConfettiFallback");
        fallbackObj.transform.position = GetWinConfettiSpawnPosition();

        ParticleSystem ps = fallbackObj.AddComponent<ParticleSystem>();

        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 1.8f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(4.5f, 9f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
        main.gravityModifier = 0.8f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 300;

        var emission = ps.emission;
        emission.enabled = false;
        emission.SetBursts(new ParticleSystem.Burst[]
        {
            new ParticleSystem.Burst(0f, 110)
        });

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 28f;
        shape.radius = 0.2f;

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color32(255, 82, 82, 255), 0f),
                new GradientColorKey(new Color32(255, 210, 64, 255), 0.25f),
                new GradientColorKey(new Color32(105, 214, 120, 255), 0.5f),
                new GradientColorKey(new Color32(80, 172, 255, 255), 0.75f),
                new GradientColorKey(new Color32(210, 110, 255, 255), 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 0.7f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = gradient;

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;

        runtimeWinConfetti = ps;
        return runtimeWinConfetti;
    }

    void StopWinConfetti()
    {
        if (winConfetti != null)
        {
            winConfetti.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        if (runtimeWinConfetti != null)
        {
            runtimeWinConfetti.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    Vector3 GetWinConfettiSpawnPosition()
    {
        if (winConfettiSpawnPoint != null)
        {
            return winConfettiSpawnPoint.position;
        }

        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            return mainCam.transform.position +
                mainCam.transform.forward * (tileSpacing * 2f) +
                mainCam.transform.up * (tileSpacing * 0.5f);
        }

        return transform.position + Vector3.up * tileSpacing;
    }

    void OnDestroy()
    {
        if (runtimeWinConfetti == null)
        {
            return;
        }

        Destroy(runtimeWinConfetti.gameObject);
        runtimeWinConfetti = null;
    }
}
