using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class GameController : MonoBehaviour
{
    [Header("UI 頁面 (請拖曳對應 Panel)")]
    public GameObject startPage;
    public GameObject gamePage;
    public GameObject catchArea;
    public GameObject whackAMoleGridPanel; // 打地鼠九宮格 UI 容器

    [Header("打地鼠 3x3 UI 方塊陣列")]
    [Tooltip("請將 9 個 UI Image 依序拉進來 (從 Grid1 到 Grid9)")]
    public Image[] whackAMoleGridCells = new Image[9];
    public Color colorDefault = Color.white;    // 預設顏色 (白色)
    public Color colorMole = Color.yellow;      // 地鼠冒頭顏色 (黃色)
    public Color colorHit = Color.green;        // 打擊成功顏色 (綠色)
    public Color colorError = Color.red;        // 踩錯顏色 (紅色)

    [Header("UI 文字元件")]
    public TextMeshProUGUI timeText;
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI infoText;

    [Header("按鈕群組")]
    public GameObject restartButton;

    [Header("遊戲四：接接樂畫面設定")]
    public RectTransform uiCatchArea;
    public GameObject prefabGoodItem;
    public GameObject prefabBadItem;
    public GameObject playerCharacter;

    // 遊戲狀態
    private int _score;
    private int _currentRound;
    private bool _isPlaying = false;
    private Demo.GameMode _currentMode;
    private Demo _demoInstance;

    private void Start()
    {
        if (Demo.Instance == null)
        {
            SceneManager.LoadScene("Normal_Demo");
            return;
        }
        _demoInstance = Demo.Instance;

        if (whackAMoleGridPanel != null)
        {
            whackAMoleGridCells = whackAMoleGridPanel.GetComponentsInChildren<UnityEngine.UI.Image>();
        }

        ShowStartPage();
    }

    #region 頁面切換與按鈕事件

    public void OnClick_StartLightningGame() => StartGame(Demo.GameMode.LightningReaction);
    public void OnClick_StartFastTapGame() => StartGame(Demo.GameMode.FastTap);
    public void OnClick_StartSingleColorGame() => StartGame(Demo.GameMode.SingleColor);
    public void OnClick_StartCatchGame() => StartGame(Demo.GameMode.CatchGame);
    public void OnClick_StartWhackAMoleGame() => StartGame(Demo.GameMode.WhackAMole);

    public void OnClick_BackToMenu()
    {
        ShowStartPage();
    }

    public void OnClick_Restart()
    {
        StartGame(_currentMode);
    }

    public void ShowStartPage()
    {
        startPage.SetActive(true);
        gamePage.SetActive(false);
        if (catchArea != null) catchArea.SetActive(false);
        if (playerCharacter != null) playerCharacter.SetActive(false);
        if (whackAMoleGridPanel != null) whackAMoleGridPanel.SetActive(false);

        ResetAllWhackAMoleGridUI();
        _demoInstance?.StopCurrentGame();
    }

    #endregion

    #region 遊戲流程

    private void StartGame(Demo.GameMode mode)
    {
        if (_demoInstance == null) return;

        startPage.SetActive(false);
        gamePage.SetActive(true);

        if (catchArea != null)
        {
            catchArea.SetActive(mode == Demo.GameMode.CatchGame);
        }

        if (whackAMoleGridPanel != null)
        {
            whackAMoleGridPanel.SetActive(mode == Demo.GameMode.WhackAMole);
        }

        if (mode == Demo.GameMode.CatchGame && playerCharacter != null)
        {
            playerCharacter.SetActive(true);
        }

        if (restartButton != null) restartButton.SetActive(false);
        if (infoText != null) infoText.text = "";

        _score = 0;
        _currentRound = 1;
        _isPlaying = true;
        _currentMode = mode;

        UpdateScoreUI();
        ResetAllWhackAMoleGridUI();

        if (mode == Demo.GameMode.FastTap || mode == Demo.GameMode.CatchGame || mode == Demo.GameMode.WhackAMole)
        {
            UpdateTimer(30f);
        }
        else
        {
            UpdateRoundDisplay();
        }

        _demoInstance.StartGameMode(mode);
    }

    public void EndGame()
    {
        _isPlaying = false;

        if (infoText != null) infoText.text = "GAME OVER";
        if (timeText != null) timeText.text = "結束";
        if (restartButton != null) restartButton.SetActive(true);
        ResetAllWhackAMoleGridUI();
    }

    #endregion

    #region 打地鼠 UI 變色控制 (核心)

    public void HighlightWhackAMoleGridUI(int row, int col, Color color)
    {
        int index = row * 3 + col;
        if (index >= 0 && index < whackAMoleGridCells.Length && whackAMoleGridCells[index] != null)
        {
            whackAMoleGridCells[index].color = color;
        }
    }

    public void ResetAllWhackAMoleGridUI()
    {
        foreach (var cell in whackAMoleGridCells)
        {
            if (cell != null) cell.color = colorDefault;
        }
    }

    #endregion

    #region UI 刷新邏輯

    public void AddScore(int value)
    {
        if (!_isPlaying) return;
        _score += value;
        UpdateScoreUI();
    }

    public void NextRound()
    {
        if (!_isPlaying) return;

        int totalRounds = (_currentMode == Demo.GameMode.LightningReaction)
                          ? _demoInstance.maxRounds
                          : _demoInstance.goNoGoMaxRounds;

        if (_currentRound >= totalRounds)
        {
            EndGame();
        }
        else
        {
            _currentRound++;

            if (_currentMode != Demo.GameMode.FastTap && _currentMode != Demo.GameMode.CatchGame && _currentMode != Demo.GameMode.WhackAMole)
            {
                UpdateRoundDisplay();
            }
        }
    }

    private void UpdateRoundDisplay()
    {
        if (timeText == null) return;

        int totalRounds = (_currentMode == Demo.GameMode.LightningReaction)
                          ? _demoInstance.maxRounds
                          : _demoInstance.goNoGoMaxRounds;

        timeText.text = $"回合:{_currentRound}/{totalRounds}";
    }

    public void UpdateTimer(float timeRemaining)
    {
        if (!_isPlaying) return;
        if (_currentMode != Demo.GameMode.FastTap && _currentMode != Demo.GameMode.CatchGame && _currentMode != Demo.GameMode.WhackAMole) return;

        if (timeText != null)
            timeText.text = $"時間:{Mathf.CeilToInt(timeRemaining)}";
    }

    private void UpdateScoreUI()
    {
        if (scoreText != null) scoreText.text = $"分數:{_score}";
    }

    #endregion
}