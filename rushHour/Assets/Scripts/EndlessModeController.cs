using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EndlessModeController : MonoBehaviour
{
    [Header("Refs")]
    public PuzzleController puzzle;
    public EnvironmentManager environmentManager;
    public Canvas targetCanvas;

    [Header("Data")]
    public TextAsset solvedLevelsJsonAsset;
    public string solvedLevelsResourceName = "all_levels_solved";
    public bool includeAlphaLevels = true;

    [Header("Progression")]
    public int startMinimumMoves = 5;
    public int minimumMovesIncreaseEvery = 3;
    public int minimumMovesIncreaseAmount = 3;
    public float nextPuzzleDelaySeconds = 2.4f;
    public float skyTimeAdvancePerPuzzle = 0.08f;

    [Header("Scoring")]
    public float parSecondsPerMinimumMove = 3.5f;
    public int baseScorePerPuzzle = 1000;

    [Header("UI")]
    public bool autoCreateUi = true;
    public bool autoCreateStartButton = true;
    public Button startButton;
    public TMP_Text statusText;
    public TMP_Text scoreboardText;
    public TMP_Text resultText;

    private readonly List<EndlessLevel> levels = new List<EndlessLevel>();
    private readonly HashSet<string> usedLevelIds = new HashSet<string>();
    private int streak;
    private int totalScore;
    private int bestScore;
    private int currentLevelNumber;
    private EndlessLevel currentLevel;
    private bool isRunning;
    private Coroutine nextPuzzleCoroutine;
    private GameObject scoreboardRoot;
    private GameObject resultRoot;

    private const string BestScorePrefKey = "rushhour.endless.bestscore";

    [Serializable]
    private class SolvedLevelRecord
    {
        public string section;
        public string tier;
        public int level_num;
        public string id;
        public string board;
        public int src;
        public int move_count;
    }

    [Serializable]
    private class SolvedLevelRecordList
    {
        public SolvedLevelRecord[] items;
    }

    private class EndlessLevel
    {
        public PuzzleController.Diff diff;
        public string id;
        public string board;
        public int sourceScore;
        public int minimumMoves;
    }

    public enum EndlessRank
    {
        F,
        D,
        C,
        B,
        A,
        S,
        SS,
        SSS
    }

    void Awake()
    {
        EnsurePuzzleReference();
        EnsureEnvironmentReference();
    }

    void Start()
    {
        LoadLevels();
        bestScore = PlayerPrefs.GetInt(BestScorePrefKey, 0);

        if (autoCreateUi)
        {
            EnsureRuntimeUi();
        }

        if (autoCreateStartButton)
        {
            EnsureStartButton();
        }

        RefreshUi();

        if (puzzle != null)
        {
            puzzle.PuzzleCompleted += OnPuzzleCompleted;
        }
    }

    void OnDestroy()
    {
        if (puzzle != null)
        {
            puzzle.PuzzleCompleted -= OnPuzzleCompleted;
        }
    }

    void Update()
    {
        RefreshStartButtonVisibility();

        if (isRunning && (puzzle == null || !puzzle.IsEndlessMode))
        {
            StopEndlessMode();
            return;
        }

        if (!isRunning || puzzle == null || puzzle.IsGameWon)
        {
            return;
        }

        RefreshUi();
    }

    public void StartEndlessMode()
    {
        EnsurePuzzleReference();
        EnsureEnvironmentReference();

        if (levels.Count == 0)
        {
            LoadLevels();
        }

        if (levels.Count == 0 || puzzle == null)
        {
            Debug.LogWarning("Endless mode could not start because no solved levels were loaded.");
            return;
        }

        if (nextPuzzleCoroutine != null)
        {
            StopCoroutine(nextPuzzleCoroutine);
            nextPuzzleCoroutine = null;
        }

        isRunning = true;
        streak = 0;
        totalScore = 0;
        currentLevelNumber = 0;
        usedLevelIds.Clear();
        currentLevel = null;

        puzzle.StartEndlessGame();

        if (environmentManager != null)
        {
            environmentManager.LoadEndlessEnvironment();
        }

        LoadNextPuzzle();
    }

    public void StopEndlessMode()
    {
        isRunning = false;

        if (nextPuzzleCoroutine != null)
        {
            StopCoroutine(nextPuzzleCoroutine);
            nextPuzzleCoroutine = null;
        }

        RefreshUi();
    }

    void OnPuzzleCompleted(PuzzleController.PuzzleCompletionResult result)
    {
        if (!isRunning || result == null || !result.endlessMode)
        {
            return;
        }

        EndlessRank rank = CalculateRank(result.movesUsed, result.minimumMoves, result.elapsedSeconds);
        int earnedScore = CalculateScore(rank, result.movesUsed, result.minimumMoves, result.elapsedSeconds);
        totalScore += earnedScore;
        streak++;

        if (totalScore > bestScore)
        {
            bestScore = totalScore;
            PlayerPrefs.SetInt(BestScorePrefKey, bestScore);
            PlayerPrefs.Save();
        }

        if (environmentManager != null)
        {
            environmentManager.AdvanceEndlessTime(skyTimeAdvancePerPuzzle);
        }

        if (resultText != null)
        {
            resultText.text = RankToString(rank) + "  +" + earnedScore + "\n" +
                "Solved in " + result.movesUsed + "/" + result.minimumMoves + " moves, " +
                FormatTime(result.elapsedSeconds);
        }

        RefreshUi();

        if (nextPuzzleCoroutine != null)
        {
            StopCoroutine(nextPuzzleCoroutine);
        }
        nextPuzzleCoroutine = StartCoroutine(LoadNextPuzzleAfterDelay());
    }

    IEnumerator LoadNextPuzzleAfterDelay()
    {
        yield return new WaitForSeconds(nextPuzzleDelaySeconds);
        LoadNextPuzzle();
        nextPuzzleCoroutine = null;
    }

    void LoadNextPuzzle()
    {
        EndlessLevel nextLevel = PickNextLevel();
        if (nextLevel == null)
        {
            Debug.LogWarning("Endless mode ran out of valid levels.");
            isRunning = false;
            RefreshUi();
            return;
        }

        currentLevel = nextLevel;
        currentLevelNumber++;
        usedLevelIds.Add(nextLevel.id);

        bool loaded = puzzle.LoadEndlessLevel(
            nextLevel.board,
            nextLevel.id,
            nextLevel.minimumMoves,
            nextLevel.sourceScore,
            nextLevel.diff,
            currentLevelNumber - 1);

        if (!loaded)
        {
            levels.Remove(nextLevel);
            LoadNextPuzzle();
            return;
        }

        if (resultText != null)
        {
            resultText.text = "";
        }

        RefreshUi();
    }

    EndlessLevel PickNextLevel()
    {
        int targetMinimumMoves = startMinimumMoves;
        if (minimumMovesIncreaseEvery > 0)
        {
            targetMinimumMoves += (currentLevelNumber / minimumMovesIncreaseEvery) * minimumMovesIncreaseAmount;
        }

        EndlessLevel best = null;
        int bestDistance = int.MaxValue;

        for (int i = 0; i < levels.Count; i++)
        {
            EndlessLevel candidate = levels[i];
            if (candidate == null || usedLevelIds.Contains(candidate.id))
            {
                continue;
            }

            int distance = Mathf.Abs(candidate.minimumMoves - targetMinimumMoves);
            if (best == null || distance < bestDistance ||
                (distance == bestDistance && candidate.minimumMoves < best.minimumMoves))
            {
                best = candidate;
                bestDistance = distance;
            }
        }

        if (best == null && levels.Count > 0)
        {
            usedLevelIds.Clear();
            return PickNextLevel();
        }

        return best;
    }

    EndlessRank CalculateRank(int movesUsed, int minimumMoves, float elapsedSeconds)
    {
        int safeMinimumMoves = Mathf.Max(1, minimumMoves);
        float moveRatio = movesUsed / (float)safeMinimumMoves;
        float parSeconds = Mathf.Max(8f, safeMinimumMoves * parSecondsPerMinimumMove);
        float timeRatio = elapsedSeconds / parSeconds;
        float performance = (moveRatio * 0.68f) + (timeRatio * 0.32f);

        if (movesUsed <= safeMinimumMoves && timeRatio <= 0.75f) return EndlessRank.SSS;
        if (performance <= 0.95f) return EndlessRank.SS;
        if (performance <= 1.05f) return EndlessRank.S;
        if (performance <= 1.2f) return EndlessRank.A;
        if (performance <= 1.45f) return EndlessRank.B;
        if (performance <= 1.75f) return EndlessRank.C;
        if (performance <= 2.15f) return EndlessRank.D;
        return EndlessRank.F;
    }

    int CalculateScore(EndlessRank rank, int movesUsed, int minimumMoves, float elapsedSeconds)
    {
        float rankMultiplier = 0.25f + ((int)rank * 0.2f);
        int safeMinimumMoves = Mathf.Max(1, minimumMoves);
        float moveBonus = Mathf.Clamp01(safeMinimumMoves / (float)Mathf.Max(1, movesUsed));
        float parSeconds = Mathf.Max(8f, safeMinimumMoves * parSecondsPerMinimumMove);
        float timeBonus = Mathf.Clamp01(parSeconds / Mathf.Max(1f, elapsedSeconds));
        float streakBonus = 1f + Mathf.Min(0.5f, streak * 0.04f);

        return Mathf.RoundToInt(baseScorePerPuzzle * rankMultiplier * (0.65f + moveBonus + (timeBonus * 0.35f)) * streakBonus);
    }

    string RankToString(EndlessRank rank)
    {
        return rank.ToString();
    }

    void LoadLevels()
    {
        levels.Clear();

        TextAsset jsonAsset = solvedLevelsJsonAsset;
        if (jsonAsset == null)
        {
            jsonAsset = Resources.Load<TextAsset>(solvedLevelsResourceName);
        }

        if (jsonAsset == null || string.IsNullOrEmpty(jsonAsset.text))
        {
            Debug.LogWarning("Endless mode could not find solved level data: " + solvedLevelsResourceName);
            return;
        }

        string wrappedJson = "{\"items\":" + jsonAsset.text.Trim() + "}";
        SolvedLevelRecordList recordList = JsonUtility.FromJson<SolvedLevelRecordList>(wrappedJson);
        if (recordList == null || recordList.items == null)
        {
            Debug.LogWarning("Endless mode could not parse solved level data.");
            return;
        }

        for (int i = 0; i < recordList.items.Length; i++)
        {
            SolvedLevelRecord record = recordList.items[i];
            if (record == null || string.IsNullOrEmpty(record.board) || string.IsNullOrEmpty(record.id))
            {
                continue;
            }

            if (!includeAlphaLevels && string.Equals(record.section, "alpha", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            EndlessLevel level = new EndlessLevel();
            level.id = record.id;
            level.board = record.board;
            level.sourceScore = record.src;
            level.minimumMoves = Mathf.Max(1, record.move_count);
            level.diff = ParseDiff(record.tier, level.minimumMoves);
            levels.Add(level);
        }

        levels.Sort(delegate(EndlessLevel a, EndlessLevel b)
        {
            int moveCompare = a.minimumMoves.CompareTo(b.minimumMoves);
            if (moveCompare != 0)
            {
                return moveCompare;
            }

            return string.CompareOrdinal(a.id, b.id);
        });
    }

    PuzzleController.Diff ParseDiff(string tier, int minimumMoves)
    {
        if (!string.IsNullOrEmpty(tier))
        {
            PuzzleController.Diff parsed;
            if (Enum.TryParse(tier, true, out parsed))
            {
                return parsed;
            }
        }

        if (minimumMoves >= 35) return PuzzleController.Diff.Expert;
        if (minimumMoves >= 24) return PuzzleController.Diff.Advanced;
        if (minimumMoves >= 12) return PuzzleController.Diff.Intermediate;
        return PuzzleController.Diff.Beginner;
    }

    void RefreshUi()
    {
        if (statusText != null)
        {
            if (!isRunning)
            {
                statusText.text = "Endless Ready";
            }
            else if (currentLevel != null && puzzle != null)
            {
                statusText.text = "Endless " + currentLevelNumber +
                    "  Min " + currentLevel.minimumMoves +
                    "  Time " + FormatTime(puzzle.CurrentElapsedTime);
            }
        }

        if (scoreboardText != null)
        {
            scoreboardText.text = "Score " + totalScore +
                "\nBest " + bestScore +
                "\nStreak " + streak;
        }

        SetGeneratedUiActive(isRunning);
    }

    string FormatTime(float seconds)
    {
        int totalSeconds = Mathf.Max(0, Mathf.FloorToInt(seconds));
        int minutes = totalSeconds / 60;
        int remainderSeconds = totalSeconds % 60;
        return minutes.ToString("00") + ":" + remainderSeconds.ToString("00");
    }

    void EnsurePuzzleReference()
    {
        if (puzzle == null)
        {
            puzzle = UnityEngine.Object.FindFirstObjectByType<PuzzleController>();
        }
    }

    void EnsureEnvironmentReference()
    {
        if (environmentManager == null)
        {
            environmentManager = UnityEngine.Object.FindFirstObjectByType<EnvironmentManager>();
        }
    }

    void EnsureRuntimeUi()
    {
        if (statusText != null && scoreboardText != null && resultText != null)
        {
            return;
        }

        Canvas canvas = targetCanvas;
        if (canvas == null)
        {
            canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
        }

        if (canvas == null)
        {
            return;
        }

        scoreboardRoot = new GameObject("EndlessScoreboard", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        scoreboardRoot.transform.SetParent(canvas.transform, false);

        RectTransform panelRect = scoreboardRoot.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 1f);
        panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot = new Vector2(1f, 1f);
        panelRect.anchoredPosition = new Vector2(-24f, -24f);
        panelRect.sizeDelta = new Vector2(260f, 118f);

        Image image = scoreboardRoot.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.45f);

        if (statusText == null)
        {
            statusText = CreateText(scoreboardRoot.transform, "EndlessStatus", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(12f, -36f), new Vector2(-12f, -8f), 18, TextAlignmentOptions.TopLeft);
        }

        if (scoreboardText == null)
        {
            scoreboardText = CreateText(scoreboardRoot.transform, "EndlessScore", new Vector2(0f, 0f), new Vector2(1f, 0.68f), new Vector2(12f, 8f), new Vector2(-12f, -4f), 16, TextAlignmentOptions.TopLeft);
        }

        if (resultText == null)
        {
            resultRoot = new GameObject("EndlessResultPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            resultRoot.transform.SetParent(canvas.transform, false);

            RectTransform resultRect = resultRoot.GetComponent<RectTransform>();
            resultRect.anchorMin = new Vector2(1f, 1f);
            resultRect.anchorMax = new Vector2(1f, 1f);
            resultRect.pivot = new Vector2(1f, 1f);
            resultRect.anchoredPosition = new Vector2(-24f, -150f);
            resultRect.sizeDelta = new Vector2(320f, 74f);

            Image resultImage = resultRoot.GetComponent<Image>();
            resultImage.color = new Color(0f, 0f, 0f, 0.45f);

            resultText = CreateText(resultRoot.transform, "EndlessResult", Vector2.zero, Vector2.one, new Vector2(12f, 8f), new Vector2(-12f, -8f), 16, TextAlignmentOptions.TopRight);
        }

        SetGeneratedUiActive(isRunning);
    }

    void EnsureStartButton()
    {
        if (startButton != null)
        {
            startButton.onClick.RemoveListener(StartEndlessMode);
            startButton.onClick.AddListener(StartEndlessMode);
            return;
        }

        if (puzzle == null || puzzle.mainMenuPanel == null)
        {
            return;
        }

        Button referenceButton = FindMenuModeButtonReference();
        TMP_Text referenceLabel = referenceButton != null ? referenceButton.GetComponentInChildren<TMP_Text>(true) : null;

        GameObject buttonObj = new GameObject("EndlessButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObj.transform.SetParent(puzzle.mainMenuPanel.transform, false);

        RectTransform rect = buttonObj.GetComponent<RectTransform>();
        rect.anchorMin = referenceButton != null ? referenceButton.GetComponent<RectTransform>().anchorMin : new Vector2(0.5f, 0.5f);
        rect.anchorMax = referenceButton != null ? referenceButton.GetComponent<RectTransform>().anchorMax : new Vector2(0.5f, 0.5f);
        rect.pivot = referenceButton != null ? referenceButton.GetComponent<RectTransform>().pivot : new Vector2(0.5f, 0.5f);
        rect.sizeDelta = referenceButton != null ? referenceButton.GetComponent<RectTransform>().sizeDelta : new Vector2(160f, 30f);
        rect.anchoredPosition = GetEndlessButtonPosition(referenceButton);

        Image image = buttonObj.GetComponent<Image>();
        Image referenceImage = referenceButton != null ? referenceButton.GetComponent<Image>() : null;
        if (referenceImage != null)
        {
            image.sprite = referenceImage.sprite;
            image.type = referenceImage.type;
            image.preserveAspect = referenceImage.preserveAspect;
            image.fillCenter = referenceImage.fillCenter;
            image.color = referenceImage.color;
        }
        else
        {
            image.color = Color.white;
        }

        startButton = buttonObj.GetComponent<Button>();
        if (referenceButton != null)
        {
            startButton.transition = referenceButton.transition;
            startButton.colors = referenceButton.colors;
            startButton.spriteState = referenceButton.spriteState;
        }
        startButton.onClick.AddListener(StartEndlessMode);

        TMP_Text label = CreateText(buttonObj.transform, "Text (TMP)", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, referenceLabel != null ? Mathf.RoundToInt(referenceLabel.fontSize) : 18, TextAlignmentOptions.Center);
        if (referenceLabel != null)
        {
            label.font = referenceLabel.font;
            label.fontSharedMaterial = referenceLabel.fontSharedMaterial;
            label.color = referenceLabel.color;
            label.enableWordWrapping = referenceLabel.enableWordWrapping;
        }
        label.text = "Endless";

        RefreshStartButtonVisibility();
    }

    Button FindMenuModeButtonReference()
    {
        if (puzzle == null || puzzle.mainMenuPanel == null)
        {
            return null;
        }

        LevelSelectCompletionUI levelSelect = puzzle.mainMenuPanel.GetComponent<LevelSelectCompletionUI>();
        if (levelSelect != null && levelSelect.entries != null && levelSelect.entries.Count > 0)
        {
            for (int i = 0; i < levelSelect.entries.Count; i++)
            {
                if (levelSelect.entries[i] != null && levelSelect.entries[i].button != null)
                {
                    return levelSelect.entries[i].button;
                }
            }
        }

        return puzzle.mainMenuPanel.GetComponentInChildren<Button>(true);
    }

    Vector2 GetEndlessButtonPosition(Button referenceButton)
    {
        Vector2 fallback = new Vector2(11f, -11f);
        if (puzzle == null || puzzle.mainMenuPanel == null)
        {
            return fallback;
        }

        LevelSelectCompletionUI levelSelect = puzzle.mainMenuPanel.GetComponent<LevelSelectCompletionUI>();
        if (levelSelect == null || levelSelect.entries == null || levelSelect.entries.Count == 0)
        {
            return referenceButton != null ? referenceButton.GetComponent<RectTransform>().anchoredPosition + new Vector2(0f, -44f) : fallback;
        }

        float x = fallback.x;
        float lowestY = float.MaxValue;
        float spacing = 44f;

        for (int i = 0; i < levelSelect.entries.Count; i++)
        {
            if (levelSelect.entries[i] == null || levelSelect.entries[i].button == null)
            {
                continue;
            }

            RectTransform entryRect = levelSelect.entries[i].button.GetComponent<RectTransform>();
            if (entryRect == null)
            {
                continue;
            }

            x = entryRect.anchoredPosition.x;
            if (entryRect.anchoredPosition.y < lowestY)
            {
                lowestY = entryRect.anchoredPosition.y;
            }
        }

        if (lowestY == float.MaxValue)
        {
            lowestY = fallback.y + spacing;
        }

        return new Vector2(x, lowestY - spacing);
    }

    void SetGeneratedUiActive(bool active)
    {
        if (scoreboardRoot != null)
        {
            scoreboardRoot.SetActive(active);
        }

        if (resultRoot != null)
        {
            resultRoot.SetActive(active && resultText != null && !string.IsNullOrEmpty(resultText.text));
        }
    }

    void RefreshStartButtonVisibility()
    {
        if (startButton == null || puzzle == null || puzzle.mainMenuPanel == null)
        {
            return;
        }

        bool visible = puzzle.mainMenuPanel.activeInHierarchy && !isRunning;
        LevelSelectCompletionUI levelSelect = puzzle.mainMenuPanel.GetComponent<LevelSelectCompletionUI>();
        if (visible && levelSelect != null && levelSelect.levelListPanel != null && levelSelect.levelListPanel.activeSelf)
        {
            visible = false;
        }

        if (startButton.gameObject.activeSelf != visible)
        {
            startButton.gameObject.SetActive(visible);
        }
    }

    TMP_Text CreateText(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, int fontSize, TextAlignmentOptions alignment)
    {
        GameObject textObj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(parent, false);

        RectTransform rect = textObj.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;

        TMP_Text text = textObj.GetComponent<TMP_Text>();
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.enableWordWrapping = false;
        return text;
    }
}
