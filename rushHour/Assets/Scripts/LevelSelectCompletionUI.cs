using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LevelSelectCompletionUI : MonoBehaviour
{
    [System.Serializable]
    public class DifficultyEntry
    {
        public Button button;
        public PuzzleController.Diff diff;
        public TMP_Text label;
    }

    [System.Serializable]
    public class LevelEntry
    {
        public Button button;
        public TMP_Text label;
    }

    [Header("Refs")]
    public PuzzleController puzzle;
    public GameObject difficultyPanel;
    public Button backToDifficultyButton;

    [Header("Difficulty Buttons")]
    public List<DifficultyEntry> entries = new List<DifficultyEntry>();

    [Header("Level Buttons")]
    public GameObject levelListPanel;
    public TMP_Text levelListTitle;
    public List<LevelEntry> levelEntries = new List<LevelEntry>();
    public bool hideUnavailableLevelButtons = true;
    public bool useSubmenuFlow = true;

    [Header("Colors")]
    public Color completedColor = new Color(0.27f, 0.74f, 0.31f, 1f);
    public Color defaultColor = Color.white;
    public Color completedTextColor = Color.white;
    public Color defaultTextColor = new Color(0.15f, 0.15f, 0.15f, 1f);
    public Color selectedDifficultyColor = new Color(0.8f, 0.9f, 1f, 1f);

    private bool hasSelectedDiff;
    private PuzzleController.Diff selectedDiff;

    bool EnsurePuzzleReference(bool logIfMissing)
    {
        if (puzzle == null)
        {
            puzzle = UnityEngine.Object.FindFirstObjectByType<PuzzleController>();
        }

        if (puzzle == null && logIfMissing)
        {
            Debug.LogWarning("LevelSelectCompletionUI could not find PuzzleController.");
        }

        return puzzle != null;
    }

    void Start()
    {
        EnsurePuzzleReference(logIfMissing: true);

        BindDifficultyButtons();
        BindBackButton();
        RefreshAll();

        if (puzzle != null)
        {
            puzzle.LevelCompletionChanged += OnLevelCompletionChanged;
        }

        ShowDifficultyMenu();
    }

    void OnEnable()
    {
        EnsurePuzzleReference(logIfMissing: false);

        //when ret from gameplay -> always open again at top level difficulty menu
        ShowDifficultyMenu();
    }

    void OnDestroy()
    {
        if (puzzle != null)
        {
            puzzle.LevelCompletionChanged -= OnLevelCompletionChanged;
        }
    }

    void BindDifficultyButtons()
    {
        for (int i = 0; i < entries.Count; i++)
        {
            DifficultyEntry entry = entries[i];
            if (entry == null || entry.button == null)
            {
                continue;
            }

            entry.button.onClick.RemoveAllListeners();

            PuzzleController.Diff capturedDiff = entry.diff;

            entry.button.onClick.AddListener(delegate
            {
                AudioManager audioManager = AudioManager.Instance;
                if (audioManager != null)
                {
                    audioManager.PlayUIClick();
                }

                if (puzzle == null)
                {
                    return;
                }

                selectedDiff = capturedDiff;
                hasSelectedDiff = true;

                if (levelListPanel != null)
                {
                    levelListPanel.SetActive(true);
                }

                if (useSubmenuFlow)
                {
                    SetDifficultyMenuVisible(false);
                }

                if (backToDifficultyButton != null)
                {
                    backToDifficultyButton.gameObject.SetActive(useSubmenuFlow);
                }

                RefreshAll();
            });
        }
    }

    void BindBackButton()
    {
        if (backToDifficultyButton == null)
        {
            return;
        }

        backToDifficultyButton.onClick.RemoveAllListeners();
        backToDifficultyButton.onClick.AddListener(delegate
        {
            AudioManager audioManager = AudioManager.Instance;
            if (audioManager != null)
            {
                audioManager.PlayUIClick();
            }

            ShowDifficultyMenu();
        });
    }

    public void ShowDifficultyMenu()
    {
        hasSelectedDiff = false;

        if (levelListPanel != null)
        {
            levelListPanel.SetActive(false);
        }

        SetDifficultyMenuVisible(true);

        if (backToDifficultyButton != null)
        {
            backToDifficultyButton.gameObject.SetActive(false);
        }

        RefreshAll();
    }

    void SetDifficultyMenuVisible(bool visible)
    {
        if (difficultyPanel != null)
        {
            difficultyPanel.SetActive(visible);
            return;
        }

        // Fallback if no panel ref: toggle the assigned difficulty buttons directly.
        for (int i = 0; i < entries.Count; i++)
        {
            DifficultyEntry entry = entries[i];
            if (entry == null || entry.button == null)
            {
                continue;
            }

            entry.button.gameObject.SetActive(visible);
        }
    }

    void OnLevelCompletionChanged(PuzzleController.Diff diff, int levelIndex, bool completed)
    {
        if (!hasSelectedDiff)
        {
            return;
        }

        if (diff == selectedDiff)
        {
            RefreshAll();
        }
    }

    public void RefreshAll()
    {
        EnsurePuzzleReference(logIfMissing: false);
        RefreshDifficultyButtons();

        if (!hasSelectedDiff)
        {
            if (levelListPanel != null)
            {
                levelListPanel.SetActive(false);
            }
            return;
        }

        RefreshLevelButtons(selectedDiff);
    }

    void RefreshDifficultyButtons()
    {
        for (int i = 0; i < entries.Count; i++)
        {
            DifficultyEntry entry = entries[i];
            if (entry == null)
            {
                continue;
            }

            bool isSelected = hasSelectedDiff && entry.diff == selectedDiff;

            if (entry.button != null)
            {
                Image image = entry.button.image;
                if (image != null)
                {
                    image.color = isSelected ? selectedDifficultyColor : defaultColor;
                }
            }

            if (entry.label != null)
            {
                entry.label.color = defaultTextColor;
            }
        }
    }

    void RefreshLevelButtons(PuzzleController.Diff diff)
    {
        int levelCount = 0;
        if (puzzle != null)
        {
            levelCount = puzzle.GetLevelCount(diff);
        }

        if (levelListTitle != null)
        {
            levelListTitle.text = diff + " Levels";
        }

        for (int i = 0; i < levelEntries.Count; i++)
        {
            LevelEntry levelEntry = levelEntries[i];
            if (levelEntry == null || levelEntry.button == null)
            {
                continue;
            }

            bool hasLevel = i < levelCount;
            bool shouldShow = hasLevel || !hideUnavailableLevelButtons;
            levelEntry.button.gameObject.SetActive(shouldShow);

            if (!shouldShow)
            {
                continue;
            }

            levelEntry.button.onClick.RemoveAllListeners();
            int capturedLevelIndex = i;

            if (hasLevel)
            {
                levelEntry.button.interactable = true;
                levelEntry.button.onClick.AddListener(delegate
                {
                    AudioManager audioManager = AudioManager.Instance;
                    if (audioManager != null)
                    {
                        audioManager.PlayUIClick();
                    }

                    if (puzzle == null)
                    {
                        return;
                    }

                    puzzle.StartGameAtLevel(diff, capturedLevelIndex);
                });
            }
            else
            {
                levelEntry.button.interactable = false;
            }

            bool completed = hasLevel && puzzle != null && puzzle.IsLevelCompleted(diff, i);

            Image image = levelEntry.button.image;
            if (image != null)
            {
                image.color = completed ? completedColor : defaultColor;
            }

            TMP_Text label = levelEntry.label;
            if (label == null)
            {
                label = levelEntry.button.GetComponentInChildren<TMP_Text>(true);
            }

            if (label != null)
            {
                label.text = (i + 1).ToString();
                label.color = completed ? completedTextColor : defaultTextColor;
            }
        }
    }
}
