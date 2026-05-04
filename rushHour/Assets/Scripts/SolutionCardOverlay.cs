using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SolutionCardOverlay : MonoBehaviour
{
    [Header("Refs")]
    public PuzzleController puzzle;

    [Header("Input")]
    public KeyCode toggleKey = KeyCode.Tab;
    public bool showOnStart = false;

    [Header("Data Source")]
    public string solutionFileName = "solution_alpha_sheet.txt";

    [Header("Visual")]
    public Vector2 cardSize = new Vector2(760f, 840f);

    // lookup by board and by level id (supports both matching paths)
    private readonly Dictionary<string, SolEntry> byBoard = new Dictionary<string, SolEntry>();
    private readonly Dictionary<string, SolEntry> byId = new Dictionary<string, SolEntry>();

    private GameObject root;
    private RectTransform cardRect;
    private TMP_Text titleText;
    private TMP_Text metaText;
    private TMP_Text boardText;
    private TMP_Text movesText;
    private ScrollRect movesScroll;
    private RectTransform movesViewportRect;
    private RectTransform movesContentRect;

    private readonly List<Image> gridCellImages = new List<Image>();
    private readonly List<TMP_Text> gridCellTexts = new List<TMP_Text>();

    private bool isVisible;
    private string lastShownId = "";
    private string lastShownBoard = "";

    private class SolEntry
    {
        public string label;
        public string tier;
        public string id;
        public string board;
        public int moves;
        public int steps;
        public List<string> solutionMoves = new List<string>();
    }

    void Start()
    {
        if (puzzle == null)
        {
            puzzle = UnityEngine.Object.FindFirstObjectByType<PuzzleController>();
        }

        LoadSolutionSheet();
        CreateUi();

        isVisible = showOnStart;
        root.SetActive(isVisible);

        if (isVisible)
        {
            RefreshCard();
        }
    }

    void Update()
    {
        if (IsTogglePressed(toggleKey))
        {
            isVisible = !isVisible;
            root.SetActive(isVisible);

            if (isVisible)
            {
                RefreshCard();
            }
        }

        if (!isVisible)
        {
            return;
        }

        string curId = "";
        string curBoard = "";

        if (puzzle != null)
        {
            curId = puzzle.CurrentLevelId;
            curBoard = puzzle.CurrentBoardString;
        }

        if (curId != lastShownId || curBoard != lastShownBoard)
        {
            RefreshCard();
        }
    }

    bool IsTogglePressed(KeyCode keyCode)
    {
        return Input.GetKeyDown(keyCode);
    }

    void LoadSolutionSheet()
    {
        // allow file near project root from either Assets depth
        string[] probePaths = new string[]
        {
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", solutionFileName)),
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", solutionFileName))
        };

        string path = "";
        for (int i = 0; i < probePaths.Length; i++)
        {
            if (File.Exists(probePaths[i]))
            {
                path = probePaths[i];
                break;
            }
        }

        if (string.IsNullOrEmpty(path))
        {
            Debug.LogWarning("Solution file not found. Checked near project root for: " + solutionFileName);
            return;
        }

        string[] lines = File.ReadAllLines(path);
        ParseLines(lines);

        Debug.Log("Solution sheet loaded. Entries by board: " + byBoard.Count + ", by id: " + byId.Count);
    }

    void ParseLines(string[] lines)
    {
        SolEntry cur = null;
        bool readingSolution = false;

        Regex headerRegex = new Regex(@"^(?<label>.+?)\s*\|\s*ID:\s*(?<id>\d+)\s*\|\s*Moves:\s*(?<moves>\d+)\s*\|\s*Steps:\s*(?<steps>\d+)", RegexOptions.Compiled);

        for (int i = 0; i < lines.Length; i++)
        {
            string raw = lines[i];
            string line = "";
            if (raw != null)
            {
                line = raw.Trim();
            }

            if (string.IsNullOrEmpty(line))
            {
                continue;
            }

            Match m = headerRegex.Match(line);
            if (m.Success)
            {
                FinalizeCurrent(cur);

                cur = new SolEntry();
                cur.label = m.Groups["label"].Value.Trim();
                cur.id = m.Groups["id"].Value.Trim();

                int parsedMoves = 0;
                int parsedSteps = 0;
                int.TryParse(m.Groups["moves"].Value, out parsedMoves);
                int.TryParse(m.Groups["steps"].Value, out parsedSteps);

                cur.moves = parsedMoves;
                cur.steps = parsedSteps;
                cur.tier = DetectTier(cur.label);

                readingSolution = false;
                continue;
            }

            if (cur == null)
            {
                continue;
            }

            if (line.StartsWith("Board:"))
            {
                cur.board = line.Substring("Board:".Length).Trim();
                continue;
            }

            if (line.StartsWith("Solution"))
            {
                readingSolution = true;
                continue;
            }

            if (line.StartsWith("########################################################################"))
            {
                readingSolution = false;
                continue;
            }

            if (readingSolution)
            {
                List<string> tokens = ParseMoveTokens(line);
                for (int t = 0; t < tokens.Count; t++)
                {
                    cur.solutionMoves.Add(tokens[t]);
                }
            }
        }

        FinalizeCurrent(cur);
    }

    void FinalizeCurrent(SolEntry entry)
    {
        if (entry == null)
        {
            return;
        }

        if (string.IsNullOrEmpty(entry.board) || string.IsNullOrEmpty(entry.id))
        {
            return;
        }

        if (!byBoard.ContainsKey(entry.board))
        {
            byBoard.Add(entry.board, entry);
        }

        if (!byId.ContainsKey(entry.id))
        {
            byId.Add(entry.id, entry);
        }
    }

    string DetectTier(string label)
    {
        if (label.IndexOf("Beginner", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "Beginner";
        }
        if (label.IndexOf("Intermediate", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "Intermediate";
        }
        if (label.IndexOf("Advanced", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "Advanced";
        }
        if (label.IndexOf("Expert", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "Expert";
        }

        return "Unknown";
    }

    List<string> ParseMoveTokens(string line)
    {
        List<string> moves = new List<string>();

        string[] parts = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            string token = parts[i].Trim();
            token = StripControlChars(token);

            if (token == "●")
            {
                continue;
            }

            if (token.Length < 2)
            {
                continue;
            }

            if (!char.IsLetter(token[0]))
            {
                continue;
            }

            // keep original token style (X→1, A<-2, etc.)
            moves.Add(token);
        }

        return moves;
    }

    string StripControlChars(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        // guards weird hidden chars from file encoding glitches
        StringBuilder sb = new StringBuilder(input.Length);
        for (int i = 0; i < input.Length; i++)
        {
            char ch = input[i];
            if (!char.IsControl(ch))
            {
                sb.Append(ch);
            }
        }

        return sb.ToString();
    }

    void CreateUi()
    {
        GameObject canvasObj = new GameObject("SolutionCardCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();

        root = new GameObject("SolutionCardRoot");
        root.transform.SetParent(canvasObj.transform, false);

        RectTransform rootRect = root.AddComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        Image rootImage = root.AddComponent<Image>();
        rootImage.color = new Color32(0, 0, 0, 145);

        GameObject card = new GameObject("Card");
        card.transform.SetParent(root.transform, false);

        cardRect = card.AddComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.anchoredPosition = Vector2.zero;
        cardRect.sizeDelta = cardSize;

        Image cardImage = card.AddComponent<Image>();
        cardImage.color = new Color32(230, 220, 185, 250);

        GameObject header = new GameObject("Header");
        header.transform.SetParent(card.transform, false);

        RectTransform headerRect = header.AddComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.anchoredPosition = Vector2.zero;
        headerRect.sizeDelta = new Vector2(0f, 98f);

        Image headerImage = header.AddComponent<Image>();
        headerImage.color = new Color32(24, 125, 62, 255);

        titleText = CreateTmpText("Title", header.transform, 42, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        RectTransform titleRect = titleText.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 0f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.offsetMin = new Vector2(18f, 0f);
        titleRect.offsetMax = new Vector2(-18f, 0f);
        titleText.color = Color.white;

        metaText = CreateTmpText("Meta", card.transform, 40, FontStyles.Bold, TextAlignmentOptions.TopLeft);
        RectTransform metaRect = metaText.GetComponent<RectTransform>();
        metaRect.anchorMin = new Vector2(0f, 1f);
        metaRect.anchorMax = new Vector2(1f, 1f);
        metaRect.pivot = new Vector2(0.5f, 1f);
        metaRect.anchoredPosition = new Vector2(0f, -108f);
        metaRect.sizeDelta = new Vector2(-36f, 152f);
        metaText.color = new Color32(52, 43, 30, 255);

        boardText = CreateTmpText("BoardText", card.transform, 28, FontStyles.Bold, TextAlignmentOptions.TopLeft);
        RectTransform boardTextRect = boardText.GetComponent<RectTransform>();
        boardTextRect.anchorMin = new Vector2(0f, 1f);
        boardTextRect.anchorMax = new Vector2(1f, 1f);
        boardTextRect.pivot = new Vector2(0.5f, 1f);
        boardTextRect.anchoredPosition = new Vector2(0f, -258f);
        boardTextRect.sizeDelta = new Vector2(-36f, 42f);
        boardText.color = new Color32(52, 43, 30, 255);

        GameObject gridHolder = new GameObject("BoardGrid");
        gridHolder.transform.SetParent(card.transform, false);
        RectTransform gridRect = gridHolder.AddComponent<RectTransform>();
        gridRect.anchorMin = new Vector2(0f, 1f);
        gridRect.anchorMax = new Vector2(0f, 1f);
        gridRect.pivot = new Vector2(0f, 1f);
        gridRect.anchoredPosition = new Vector2(18f, -308f);
        gridRect.sizeDelta = new Vector2(360f, 360f);

        Image gridBack = gridHolder.AddComponent<Image>();
        gridBack.color = new Color32(204, 192, 156, 255);

        GridLayoutGroup gridLayout = gridHolder.AddComponent<GridLayoutGroup>();
        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = 6;
        gridLayout.cellSize = new Vector2(56f, 56f);
        gridLayout.spacing = new Vector2(4f, 4f);
        gridLayout.padding = new RectOffset(8, 8, 8, 8);
        gridLayout.childAlignment = TextAnchor.UpperLeft;

        for (int i = 0; i < 36; i++)
        {
            GameObject cell = new GameObject("Cell_" + i);
            cell.transform.SetParent(gridHolder.transform, false);

            Image cellImage = cell.AddComponent<Image>();
            cellImage.color = new Color32(117, 115, 154, 255);
            gridCellImages.Add(cellImage);

            TMP_Text cellText = CreateTmpText("CellText", cell.transform, 30, FontStyles.Bold, TextAlignmentOptions.Center);
            RectTransform cellTextRect = cellText.GetComponent<RectTransform>();
            cellTextRect.anchorMin = Vector2.zero;
            cellTextRect.anchorMax = Vector2.one;
            cellTextRect.offsetMin = Vector2.zero;
            cellTextRect.offsetMax = Vector2.zero;
            cellText.color = new Color32(239, 239, 248, 255);
            cellText.text = ".";

            gridCellTexts.Add(cellText);
        }

        TMP_Text solutionHeading = CreateTmpText("SolutionHeading", card.transform, 30, FontStyles.Bold, TextAlignmentOptions.TopLeft);
        RectTransform headingRect = solutionHeading.GetComponent<RectTransform>();
        headingRect.anchorMin = new Vector2(0f, 1f);
        headingRect.anchorMax = new Vector2(0f, 1f);
        headingRect.pivot = new Vector2(0f, 1f);
        headingRect.anchoredPosition = new Vector2(392f, -308f);
        headingRect.sizeDelta = new Vector2(340f, 38f);
        solutionHeading.color = new Color32(52, 43, 30, 255);
        solutionHeading.text = "Solution";

        GameObject scrollObj = new GameObject("MovesScroll");
        scrollObj.transform.SetParent(card.transform, false);
        RectTransform scrollRect = scrollObj.AddComponent<RectTransform>();
        scrollRect.anchorMin = new Vector2(0f, 0f);
        scrollRect.anchorMax = new Vector2(1f, 1f);
        scrollRect.offsetMin = new Vector2(386f, 16f);
        scrollRect.offsetMax = new Vector2(-16f, -354f);

        Image scrollBack = scrollObj.AddComponent<Image>();
        scrollBack.color = new Color32(214, 203, 170, 255);

        ScrollRect scroll = scrollObj.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        movesScroll = scroll;

        GameObject viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollObj.transform, false);
        movesViewportRect = viewport.AddComponent<RectTransform>();
        movesViewportRect.anchorMin = Vector2.zero;
        movesViewportRect.anchorMax = Vector2.one;
        movesViewportRect.offsetMin = new Vector2(8f, 8f);
        movesViewportRect.offsetMax = new Vector2(-8f, -8f);

        Image viewportImage = viewport.AddComponent<Image>();
        viewportImage.color = new Color32(255, 255, 255, 8);
        Mask viewportMask = viewport.AddComponent<Mask>();
        viewportMask.showMaskGraphic = false;

        GameObject content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);
        movesContentRect = content.AddComponent<RectTransform>();
        movesContentRect.anchorMin = new Vector2(0f, 1f);
        movesContentRect.anchorMax = new Vector2(1f, 1f);
        movesContentRect.pivot = new Vector2(0.5f, 1f);
        movesContentRect.anchoredPosition = Vector2.zero;
        movesContentRect.sizeDelta = new Vector2(0f, 100f);

        movesText = CreateTmpText("Moves", content.transform, 34, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        RectTransform movesRect = movesText.GetComponent<RectTransform>();
        movesRect.anchorMin = new Vector2(0f, 1f);
        movesRect.anchorMax = new Vector2(1f, 1f);
        movesRect.pivot = new Vector2(0.5f, 1f);
        movesRect.anchoredPosition = Vector2.zero;
        movesRect.sizeDelta = new Vector2(-12f, 100f);
        movesText.textWrappingMode = TextWrappingModes.Normal;
        movesText.color = new Color32(66, 49, 29, 255);

        scroll.viewport = movesViewportRect;
        scroll.content = movesContentRect;
    }

    TMP_Text CreateTmpText(string name, Transform parent, int size, FontStyles style, TextAlignmentOptions align)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);

        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.localScale = Vector3.one;

        TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.alignment = align;
        tmp.text = "";

        return tmp;
    }

    void RefreshCard()
    {
        SolEntry entry = FindCurrentEntry();
        bool levelChanged = false;

        if (entry != null)
        {
            if (entry.id != lastShownId || entry.board != lastShownBoard)
            {
                levelChanged = true;
            }
        }

        if (entry == null)
        {
            titleText.text = "Solution Card";
            metaText.text = "No solution found for current puzzle.\nCheck ID/board mapping in solution_master_sheet.txt.";
            boardText.text = "Board";
            movesText.text = "";
            UpdateBoardGrid("");
            ResizeMovesContent();
            ResetMovesScrollToTop();

            lastShownId = "";
            lastShownBoard = "";
            if (puzzle != null)
            {
                lastShownId = puzzle.CurrentLevelId;
                lastShownBoard = puzzle.CurrentBoardString;
            }
            return;
        }

        titleText.text = entry.label;

        StringBuilder meta = new StringBuilder();
        meta.Append("Tier: ").Append(entry.tier).Append("\n");
        meta.Append("ID: ").Append(entry.id).Append("\n");
        meta.Append("Moves: ").Append(entry.moves).Append("   Steps: ").Append(entry.steps);
        metaText.text = meta.ToString();

        boardText.text = "Board";
        UpdateBoardGrid(entry.board);

        StringBuilder moveSb = new StringBuilder();
        moveSb.Append("\n");

        for (int i = 0; i < entry.solutionMoves.Count; i++)
        {
            moveSb.Append(i + 1).Append(". ").Append(GetDisplayMove(entry.solutionMoves[i])).Append("\n");
        }

        moveSb.Append("\nSolved");
        movesText.text = moveSb.ToString();
        ResizeMovesContent();

        if (levelChanged)
        {
            // only reset scroll on level change, not every open/refresh
            ResetMovesScrollToTop();
        }

        lastShownId = "";
        lastShownBoard = "";
        if (puzzle != null)
        {
            lastShownId = puzzle.CurrentLevelId;
            lastShownBoard = puzzle.CurrentBoardString;
        }
    }

    void ResetMovesScrollToTop()
    {
        if (movesScroll == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();

        if (movesContentRect != null)
        {
            movesContentRect.anchoredPosition = Vector2.zero;
        }

        movesScroll.verticalNormalizedPosition = 1f;
    }

    void UpdateBoardGrid(string board)
    {
        if (string.IsNullOrEmpty(board) || board.Length != 36)
        {
            for (int i = 0; i < gridCellTexts.Count; i++)
            {
                gridCellTexts[i].text = ".";
                gridCellTexts[i].color = new Color32(219, 219, 228, 255);
            }

            for (int i = 0; i < gridCellImages.Count; i++)
            {
                gridCellImages[i].color = new Color32(117, 115, 154, 255);
            }

            return;
        }

        for (int i = 0; i < 36; i++)
        {
            char ch = board[i];
            bool isEmpty = false;
            if (ch == 'o' || ch == 'x')
            {
                isEmpty = true;
            }

            if (isEmpty)
            {
                gridCellTexts[i].text = ".";
                gridCellTexts[i].color = new Color32(217, 217, 228, 255);
                gridCellImages[i].color = new Color32(117, 115, 154, 255);
            }
            else
            {
                gridCellTexts[i].text = GetDisplayPiece(ch);
                gridCellTexts[i].color = new Color32(40, 32, 23, 255);
                gridCellImages[i].color = GetPieceColor(ch);
            }
        }
    }

    string GetDisplayPiece(char piece)
    {
        return piece.ToString();
    }

    string GetDisplayMove(string token)
    {
        return token;
    }

    int DisplayPieceToCarId(char piece)
    {
        if (piece == 'X')
        {
            return 0;
        }

        if (piece >= 'A' && piece <= 'Y')
        {
            return (piece - 'A') + 1;
        }

        return -1;
    }

    Color32 GetPieceColor(char piece)
    {
        int carId = DisplayPieceToCarId(piece);
        if (carId >= 0 && puzzle != null)
        {
            Color32 liveColor;
            // first choice: live in-game color from PuzzleController map
            if (puzzle.GetCarColorById(carId, out liveColor))
            {
                return liveColor;
            }
        }

        // fallback palette if live map unavailable
        if (piece == 'X') return new Color32(220, 70, 70, 255);
        if (piece == 'A') return new Color32(165, 218, 245, 255);
        if (piece == 'B') return new Color32(118, 140, 236, 255);
        if (piece == 'C') return new Color32(201, 78, 90, 255);
        if (piece == 'D') return new Color32(153, 109, 204, 255);
        if (piece == 'E') return new Color32(80, 184, 196, 255);
        if (piece == 'F') return new Color32(149, 77, 171, 255);
        if (piece == 'G') return new Color32(228, 150, 73, 255);
        if (piece == 'H') return new Color32(190, 154, 224, 255);
        if (piece == 'I') return new Color32(245, 187, 214, 255);
        if (piece == 'J') return new Color32(128, 213, 167, 255);
        if (piece == 'K') return new Color32(245, 140, 175, 255);
        if (piece == 'L') return new Color32(240, 172, 112, 255);
        if (piece == 'M') return new Color32(116, 200, 220, 255);
        if (piece == 'N') return new Color32(214, 169, 115, 255);

        return new Color32(196, 187, 155, 255);
    }

    void ResizeMovesContent()
    {
        if (movesText == null || movesContentRect == null || movesViewportRect == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();

        float preferredHeight = movesText.preferredHeight + 16f;
        float viewportHeight = movesViewportRect.rect.height;
        float targetHeight = viewportHeight;

        if (preferredHeight > targetHeight)
        {
            targetHeight = preferredHeight;
        }

        movesContentRect.sizeDelta = new Vector2(0f, targetHeight);

        RectTransform textRect = movesText.rectTransform;
        textRect.sizeDelta = new Vector2(-12f, targetHeight);
    }

    SolEntry FindCurrentEntry()
    {
        if (puzzle == null)
        {
            return null;
        }

        string id = puzzle.CurrentLevelId;
        if (!string.IsNullOrEmpty(id) && byId.ContainsKey(id))
        {
            return byId[id];
        }

        string board = puzzle.CurrentBoardString;
        if (!string.IsNullOrEmpty(board) && byBoard.ContainsKey(board))
        {
            return byBoard[board];
        }

        return null;
    }
}
