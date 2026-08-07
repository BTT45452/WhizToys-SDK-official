using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using Plugins.WhizToys;
using Plugins.WhizToys.Models;

public class Demo : MonoBehaviour
{
    public static Demo Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        InitializeWhizToys();
    }

    [Header("基本設定")]
    public bool isMobile = false;
    public string autoConnectName = "WTS2";

    [Header("音效設定")]
    public AudioSource audioSource;
    public AudioClip clipSuccess;
    public AudioClip clipError;
    public AudioClip clipCountdown;

    [Header("遊戲參數設定")]
    public int maxRounds = 15;
    public int goNoGoMaxRounds = 15;
    public float initialReactionTime = 2.5f;

    [Header("遊戲四：接接樂與畫面物品設定")]
    public float catchGameDuration = 30f;
    public float catchDropSpeed = 0.5f;
    public RectTransform uiCatchArea;
    public GameObject prefabGoodItem;
    public GameObject prefabBadItem;
    public GameObject playerCharacter;

    public WhizToysMap ConnectedDeviceMap { get; private set; }
    private WhizToys _whizToys;
    private ScanUI _scanUI;

    private bool _isScanning = false;
    private bool _gameRunning = false;
    private bool _isLightOn = false;

    public enum GameMode { None, LightningReaction, TimerSequence, ManualControl, FastTap, SingleColor, CatchGame, WhackAMole }
    private GameMode _currentGameMode = GameMode.None;
    private string _currentLogFilePath;
    private float _lightOnTime;

    private int _targetRow = -1, _targetCol = -1;
    private HashSet<Vector2Int> _pressedPads = new HashSet<Vector2Int>();

    private bool _isGoNoGoSetup = false;
    private List<int> _colorPalette = new List<int> { 1, 15, 25, 41, 50 };
    private Dictionary<Vector2Int, int> _activePadsColorMap = new Dictionary<Vector2Int, int>();
    private int _targetColorIndex;

    private int _catchCurrentRow = -1;
    private int _catchCurrentCol = -1;
    private bool _isCurrentItemGood = true;

    private struct FallingItem
    {
        public int row;
        public bool isGood;
        public GameObject uiObject;
        public RectTransform rect;
        public bool isSettled;
    }
    private List<FallingItem> _activeFallingItems = new List<FallingItem>();
    private bool _isDualDropMode = false;

    private void InitializeWhizToys()
    {
        if (_whizToys != null) return;
        _whizToys = isMobile ? (WhizToys)new WhizToys_Mobile() : new WhizToys_Windows();
        _whizToys.OnInitSuccess = () => Debug.Log("[Demo] SDK 初始化成功");
        _whizToys.OnScanDevice = OnScanDeviceFound;
        _whizToys.OnScanEnd = () => { _isScanning = false; };
        _whizToys.OnConnected = OnDeviceConnected;
        _whizToys.OnDisconnect = () => Debug.LogWarning("[Demo] 裝置斷線");
        _whizToys.OnReceiveSignal = HandleSensorSignal;
        _whizToys.Initialize();
    }

    private void Update()
    {
        if (!_gameRunning) return;

        if (Input.GetKeyDown(KeyCode.Keypad7) || Input.GetKeyDown(KeyCode.Alpha7)) ProcessGameplayInput(0, 0);
        if (Input.GetKeyDown(KeyCode.Keypad8) || Input.GetKeyDown(KeyCode.Alpha8)) ProcessGameplayInput(0, 1);
        if (Input.GetKeyDown(KeyCode.Keypad9) || Input.GetKeyDown(KeyCode.Alpha9)) ProcessGameplayInput(0, 2);

        if (Input.GetKeyDown(KeyCode.Keypad4) || Input.GetKeyDown(KeyCode.Alpha4)) ProcessGameplayInput(1, 0);
        if (Input.GetKeyDown(KeyCode.Keypad5) || Input.GetKeyDown(KeyCode.Alpha5)) ProcessGameplayInput(1, 1);
        if (Input.GetKeyDown(KeyCode.Keypad6) || Input.GetKeyDown(KeyCode.Alpha6)) ProcessGameplayInput(1, 2);

        if (Input.GetKeyDown(KeyCode.Keypad1) || Input.GetKeyDown(KeyCode.Alpha1)) ProcessGameplayInput(2, 0);
        if (Input.GetKeyDown(KeyCode.Keypad2) || Input.GetKeyDown(KeyCode.Alpha2)) ProcessGameplayInput(2, 1);
        if (Input.GetKeyDown(KeyCode.Keypad3) || Input.GetKeyDown(KeyCode.Alpha3)) ProcessGameplayInput(2, 2);
    }

    public void StartScan()
    {
        if (_whizToys == null) return;
        _whizToys.StartScan(5);
        _isScanning = true;
        _scanUI = null;
    }

    public void Connect(string address)
    {
        if (_whizToys == null) return;
        _isScanning = false;
        _whizToys.Connect(address);
    }

    private void OnScanDeviceFound(string address, string deviceName)
    {
        if (_isScanning && !string.IsNullOrEmpty(autoConnectName) && deviceName == autoConnectName)
        {
            Connect(address);
            return;
        }
        if (_isScanning)
        {
            if (_scanUI == null) _scanUI = FindFirstObjectByType<ScanUI>();
            _scanUI?.AddDeviceToList(address, deviceName);
        }
    }

    private void OnDeviceConnected(WhizToysMap map)
    {
        ConnectedDeviceMap = map;
        SceneManager.LoadScene("MainPage");
    }

    private void HandleSensorSignal(List<WhizToysSignal> values)
    {
        if (!_gameRunning && !_isGoNoGoSetup) return;
        foreach (var signal in values)
        {
            var pos = new Vector2Int(signal.Layout.Row, signal.Layout.Column);
            bool isPressed = signal.Pressures.Any(p => p > 0);

            if (isPressed)
            {
                if (!_pressedPads.Contains(pos))
                {
                    _pressedPads.Add(pos);
                    ProcessGameplayInput(pos.x, pos.y);
                }
            }
            else
            {
                if (_pressedPads.Contains(pos)) _pressedPads.Remove(pos);
            }
        }
    }

    public void StartGameMode(GameMode mode)
    {
        StopCurrentGame();
        _currentGameMode = mode;
        _gameRunning = true;

        if (!CreateLogFile(mode)) { _gameRunning = false; return; }

        switch (mode)
        {
            case GameMode.LightningReaction: StartCoroutine(Routine_LightningReaction()); break;
            case GameMode.FastTap: StartCoroutine(Routine_FastTap()); break;
            case GameMode.SingleColor: StartCoroutine(Routine_GoNoGo()); break;
            case GameMode.CatchGame: StartCoroutine(Routine_CatchGame()); break;
            case GameMode.WhackAMole: StartCoroutine(Routine_WhackAMole()); break;
        }
    }

    public void StopCurrentGame()
    {
        _gameRunning = false;
        StopAllCoroutines();
        if (_whizToys != null && ConnectedDeviceMap != null) TurnOffAllLights();

        var gc = FindFirstObjectByType<GameController>();
        gc?.ResetAllWhackAMoleGridUI();

        _currentGameMode = GameMode.None;
        _isGoNoGoSetup = false;
        _pressedPads.Clear();

        if (uiCatchArea != null)
        {
            foreach (Transform child in uiCatchArea) Destroy(child.gameObject);
        }
        _activeFallingItems.Clear();
        _isDualDropMode = false;
    }

    // 遊戲一
    private IEnumerator Routine_LightningReaction()
    {
        float reactionLimit = initialReactionTime;
        var gc = FindFirstObjectByType<GameController>();
        for (int round = 1; round <= maxRounds; round++)
        {
            if (!_gameRunning) yield break;
            yield return new WaitUntil(() => _pressedPads.Count == 0);
            if (!_gameRunning) yield break;

            bool isRoundWon = false;
            LightUpRandomPad();
            float timer = 0f;
            while (timer < reactionLimit && _isLightOn && _gameRunning)
            {
                timer += Time.deltaTime;
                yield return null;
            }
            if (_isLightOn) { PlaySound(clipError); TurnOffAllLights(); WriteLog("false", 0.00f); }
            else { isRoundWon = true; }

            if (isRoundWon) reactionLimit = Mathf.Max(reactionLimit * 0.95f, 0.5f);
            gc?.NextRound();
            yield return new WaitForSeconds(0.5f);
        }
        EndGameCleanup(gc);
    }

    // 遊戲二
    private IEnumerator Routine_FastTap()
    {
        float timeLeft = 30f;
        var gc = FindFirstObjectByType<GameController>();
        bool hasPlayedCountdown = false;
        while (timeLeft > 0 && _gameRunning)
        {
            LightUpRandomPad();
            float padDuration = 2.0f;
            while (padDuration > 0 && _isLightOn && _gameRunning)
            {
                float dt = Time.deltaTime;
                padDuration -= dt; timeLeft -= dt;
                if (timeLeft < 0) timeLeft = 0;
                if (timeLeft <= 10.0f && !hasPlayedCountdown) { hasPlayedCountdown = true; PlaySound(clipCountdown); }
                gc?.UpdateTimer(timeLeft);
                yield return null;
            }
            if (_isLightOn) TurnOffAllLights();
            yield return new WaitForSeconds(0.2f);
        }
        EndGameCleanup(gc);
    }

    // 遊戲三
    private IEnumerator Routine_GoNoGo()
    {
        var gc = FindFirstObjectByType<GameController>();
        var allPads = GetAllActivePads();
        if (allPads.Count == 0) yield break;

        _isGoNoGoSetup = true;
        TurnOffAllLights();
        _activePadsColorMap.Clear();
        _pressedPads.Clear();
        _targetColorIndex = _colorPalette[UnityEngine.Random.Range(0, _colorPalette.Count)];

        var setupCommands = new List<WhizToysSendModel>();
        foreach (var p in allPads)
        {
            setupCommands.Add(CreateLightCommand(p.x, p.y, _targetColorIndex));
            _activePadsColorMap[p] = _targetColorIndex;
        }
        _whizToys.WriteSignals(setupCommands);

        while (_isGoNoGoSetup && _gameRunning) { if (_activePadsColorMap.Count == 0) break; yield return null; }
        _isGoNoGoSetup = false;
        if (!_gameRunning) yield break;
        TurnOffAllLights();
        yield return new WaitForSeconds(0.5f);

        for (int round = 1; round <= goNoGoMaxRounds; round++)
        {
            if (!_gameRunning) yield break;
            yield return new WaitUntil(() => _pressedPads.Count == 0);
            TurnOffAllLights();
            _activePadsColorMap.Clear();

            int maxCount = Mathf.Min(6, allPads.Count);
            int minCount = Mathf.Min(3, maxCount);
            int countToLight = UnityEngine.Random.Range(minCount, maxCount + 1);

            for (int i = 0; i < allPads.Count; i++)
            {
                int rnd = UnityEngine.Random.Range(i, allPads.Count);
                (allPads[i], allPads[rnd]) = (allPads[rnd], allPads[i]);
            }

            var batchCommands = new List<WhizToysSendModel>();
            int goCount = 0;
            for (int i = 0; i < countToLight; i++)
            {
                Vector2Int pos = allPads[i];
                int color;
                if (UnityEngine.Random.Range(0, 3) == 0) { color = _targetColorIndex; goCount++; }
                else { do { color = _colorPalette[UnityEngine.Random.Range(0, _colorPalette.Count)]; } while (color == _targetColorIndex); }
                batchCommands.Add(CreateLightCommand(pos.x, pos.y, color));
                _activePadsColorMap[pos] = color;
            }
            _whizToys.WriteSignals(batchCommands);

            float waitTime = 1.5f; _lightOnTime = Time.time;
            while (waitTime > 0 && _gameRunning) { waitTime -= Time.deltaTime; yield return null; }

            bool hasRemainingGo = _activePadsColorMap.Values.Any(c => c == _targetColorIndex);
            if (hasRemainingGo) { PlaySound(clipError); WriteLog("false", 0.00f); }
            else if (goCount == 0) { if (_activePadsColorMap.Count == countToLight) { gc?.AddScore(1); PlaySound(clipSuccess); WriteLog("true", 0.00f); } }
            TurnOffAllLights(); gc?.NextRound(); yield return new WaitForSeconds(0.5f);
        }
        EndGameCleanup(gc);
    }

    // 遊戲四
    private IEnumerator Routine_CatchGame()
    {
        float timeLeft = catchGameDuration;
        var gc = FindFirstObjectByType<GameController>();
        bool hasPlayedCountdown = false;

        if (playerCharacter != null) playerCharacter.gameObject.SetActive(true);

        while (timeLeft > 0 && _gameRunning)
        {
            _activeFallingItems.Clear();
            TurnOffAllLights();

            float speedMultiplier = 1.0f;
            _isDualDropMode = false;

            if (timeLeft <= 10.0f)
            {
                speedMultiplier = 2.0f;
                _isDualDropMode = true;
            }
            else if (timeLeft <= 20.0f)
            {
                speedMultiplier = 1.5f;
            }

            float currentDropDuration = (catchDropSpeed * 3f) / speedMultiplier;
            var lightCommands = new List<WhizToysSendModel>();

            if (_isDualDropMode)
            {
                bool leftIsGood = UnityEngine.Random.value > 0.5f;
                bool rightIsGood = !leftIsGood;

                CreateFallingItem(0, leftIsGood, gc, lightCommands);
                CreateFallingItem(1, rightIsGood, gc, lightCommands);
            }
            else
            {
                int side = UnityEngine.Random.Range(0, 2);
                bool isGood = UnityEngine.Random.value > 0.3f;
                CreateFallingItem(side, isGood, gc, lightCommands);

                _catchCurrentRow = side;
                _catchCurrentCol = 0;
                _isCurrentItemGood = isGood;
            }

            _whizToys?.WriteSignals(lightCommands);
            _isLightOn = true;
            _lightOnTime = Time.time;

            float stepTimer = currentDropDuration;

            while (stepTimer > 0 && _isLightOn && _gameRunning)
            {
                float dt = Time.deltaTime;
                stepTimer -= dt; timeLeft -= dt;
                if (timeLeft < 0) timeLeft = 0;

                if (timeLeft <= 10.0f && !hasPlayedCountdown) { hasPlayedCountdown = true; PlaySound(clipCountdown); }
                gc?.UpdateTimer(timeLeft);

                float currentProgress = stepTimer / currentDropDuration;
                float areaHeight = (gc != null && gc.uiCatchArea != null) ? gc.uiCatchArea.rect.height : 600f;
                float posY = (areaHeight / 2f) - ((1f - currentProgress) * areaHeight);

                for (int i = 0; i < _activeFallingItems.Count; i++)
                {
                    var item = _activeFallingItems[i];
                    if (item.isSettled) continue;

                    if (item.rect != null)
                    {
                        float itemX = (item.row == 0) ? -150f : 150f;
                        item.rect.anchoredPosition = new Vector2(itemX, posY);
                    }

                    if (posY <= -330f && posY >= -410f)
                    {
                        RectTransform playerRt = playerCharacter != null ? playerCharacter.GetComponent<RectTransform>() : null;
                        if (playerRt == null && gc != null && gc.playerCharacter != null) playerRt = gc.playerCharacter.GetComponent<RectTransform>();

                        if (playerRt != null)
                        {
                            float targetX = (item.row == 0) ? -150f : 150f;
                            if (Mathf.Abs(playerRt.anchoredPosition.x - targetX) < 15f)
                            {
                                item.isSettled = true;
                                _activeFallingItems[i] = item;
                                HandleItemSettlement(item, true, gc);
                            }
                        }
                    }
                }

                if (_activeFallingItems.Any(item => item.isSettled))
                {
                    _isLightOn = false;
                    break;
                }

                yield return null;
            }

            for (int i = 0; i < _activeFallingItems.Count; i++)
            {
                var item = _activeFallingItems[i];
                if (!item.isSettled)
                {
                    item.isSettled = true;
                    HandleItemSettlement(item, false, gc);
                }
                if (item.uiObject != null) Destroy(item.uiObject);
            }

            yield return new WaitForSeconds(0.3f);
        }

        EndGameCleanup(gc);
    }

    // 遊戲五：打地鼠 (Whack-A-Mole)
    private IEnumerator Routine_WhackAMole()
    {
        float gameTimer = 30f;
        var gc = FindFirstObjectByType<GameController>();
        bool hasPlayedCountdown = false;

        // 取得所有有連線的地墊
        var activePads = GetAllActivePads();

        // 基礎停留時間放寬至 2.5 秒，給予充足的邁步時間
        float moleStayDuration = 2.5f;

        // 記錄上一次（或目前站在上面）的有效位置
        Vector2Int lastValidPad = new Vector2Int(-1, -1);

        while (gameTimer > 0 && _gameRunning)
        {
            TurnOffAllLights();
            gc?.ResetAllWhackAMoleGridUI();

            // 邁步過渡緩衝時間：給玩家 0.4 秒轉移重心，這段期間舊位置是安全區
            yield return new WaitForSeconds(0.4f);

            Vector2Int newTargetPos;

            if (activePads != null && activePads.Count > 1)
            {
                // 【人體工學邏輯】：強制抽出一個「跟目前位置不一樣」的新位置，引導玩家移動！
                do
                {
                    newTargetPos = activePads[UnityEngine.Random.Range(0, activePads.Count)];
                }
                while (newTargetPos == lastValidPad && activePads.Count > 1);
            }
            else
            {
                // 單機測試 fallback
                newTargetPos = new Vector2Int(UnityEngine.Random.Range(0, 3), UnityEngine.Random.Range(0, 3));
            }

            _targetRow = newTargetPos.x;
            _targetCol = newTargetPos.y;

            // 點亮實體地墊與 UI
            if (_whizToys != null)
            {
                _whizToys.WriteSignals(new List<WhizToysSendModel> { CreateLightCommand(_targetRow, _targetCol, 41) });
            }
            if (gc != null)
            {
                gc.HighlightWhackAMoleGridUI(_targetRow, _targetCol, gc.colorMole);
            }

            _isLightOn = true;
            _lightOnTime = Time.time;

            float currentMoleTimer = moleStayDuration;
            while (currentMoleTimer > 0 && _isLightOn && _gameRunning)
            {
                float dt = Time.deltaTime;
                currentMoleTimer -= dt;
                gameTimer -= dt;

                if (gameTimer < 0) gameTimer = 0;
                if (gameTimer <= 10.0f && !hasPlayedCountdown) { hasPlayedCountdown = true; PlaySound(clipCountdown); }

                gc?.UpdateTimer(gameTimer);
                yield return null;
            }

            // 時間到沒踩中
            if (_isLightOn)
            {
                TurnOffAllLights();
                gc?.ResetAllWhackAMoleGridUI();
                WriteLog("missed", 0.00f);
            }
            else
            {
                // 踩中了，把當前位置記錄為「上一位置（允許站立）」
                lastValidPad = new Vector2Int(_targetRow, _targetCol);
            }
        }

        gc?.ResetAllWhackAMoleGridUI();
        EndGameCleanup(gc);
    }

    private void CreateFallingItem(int side, bool isGood, GameController gc, List<WhizToysSendModel> commands)
    {
        GameObject spawnPrefab = isGood ? (gc != null ? gc.prefabGoodItem : prefabGoodItem) : (gc != null ? gc.prefabBadItem : prefabBadItem);
        int lightColor = isGood ? 15 : 1;

        GameObject uiItem = null;
        RectTransform itemRect = null;

        if (gc != null && gc.uiCatchArea != null && spawnPrefab != null)
        {
            uiItem = Instantiate(spawnPrefab, gc.uiCatchArea);
            itemRect = uiItem.GetComponent<RectTransform>();
            float areaHeight = gc.uiCatchArea.rect.height;
            itemRect.anchoredPosition = new Vector2((side == 0) ? -150f : 150f, areaHeight / 2f);
        }

        _activeFallingItems.Add(new FallingItem
        {
            row = side,
            isGood = isGood,
            uiObject = uiItem,
            rect = itemRect,
            isSettled = false
        });

        if (side == 0)
        {
            commands.Add(CreateLightCommand(0, 0, lightColor));
        }
        else
        {
            commands.Add(CreateLightCommand(1, 0, lightColor));
            commands.Add(CreateLightCommand(0, 1, lightColor));
        }
    }

    private void HandleItemSettlement(FallingItem item, bool hitByGhost, GameController gc)
    {
        float reaction = Time.time - _lightOnTime;

        if (hitByGhost)
        {
            if (item.isGood)
            {
                gc?.AddScore(1);
                PlaySound(clipSuccess);
                _whizToys?.WriteSignals(new List<WhizToysSendModel> { CreateLightCommand(item.row, 0, 25) });
                WriteLog("gold_caught", reaction);
            }
            else
            {
                gc?.AddScore(-1);
                PlaySound(clipError);
                _whizToys?.WriteSignals(new List<WhizToysSendModel> { CreateLightCommand(item.row, 0, 1) });
                WriteLog("bomb_hit", reaction);
            }
        }
        else
        {
            if (item.isGood)
            {
                WriteLog("gold_missed", 0.00f);
            }
            else
            {
                WriteLog("bomb_avoided_safely", 0.00f);
            }
        }
    }

    private void ProcessGameplayInput(int row, int col)
    {
        Debug.Log($"[地墊測試] 踩下位置 -> 橫排(Row): {row} | 直行(Col): {col}");
        var gc = FindFirstObjectByType<GameController>();

        if (_currentGameMode == GameMode.LightningReaction)
        {
            if (_gameRunning && _isLightOn && row == _targetRow && col == _targetCol)
            {
                float reaction = Time.time - _lightOnTime;
                gc?.AddScore(1); PlaySound(clipSuccess); TurnOffAllLights(); WriteLog("true", reaction);
            }
            else if (_gameRunning && _isLightOn) { PlaySound(clipError); TurnOffAllLights(); WriteLog("false", 0.00f); }
        }
        else if (_currentGameMode == GameMode.FastTap)
        {
            if (_gameRunning && _isLightOn && row == _targetRow && col == _targetCol)
            {
                float reaction = Time.time - _lightOnTime;
                gc?.AddScore(1); PlaySound(clipSuccess); TurnOffAllLights(); WriteLog("true", reaction);
            }
            else if (_gameRunning && _isLightOn) { PlaySound(clipError); ShowErrorFeedback(row, col); WriteLog("false", 0.00f); }
        }
        else if (_currentGameMode == GameMode.WhackAMole)
        {
            // 踩中正確的地鼠位置
            if (_gameRunning && _isLightOn && row == _targetRow && col == _targetCol)
            {
                float reaction = Time.time - _lightOnTime;
                gc?.AddScore(1);
                PlaySound(clipSuccess);

                // 畫面與地墊亮綠燈表示成功
                if (gc != null) gc.HighlightWhackAMoleGridUI(row, col, gc.colorHit);
                _whizToys?.WriteSignals(new List<WhizToysSendModel> { CreateLightCommand(row, col, 25) });

                _isLightOn = false; // 標記已擊中
                WriteLog("hit", reaction);
            }
            
            else if (_gameRunning && _isLightOn)
            {                
                Debug.Log($"[移動緩衝] 玩家踩踏位置 ({row}, {col}) 為非目標點，不計為失誤。");
            }
        }
        else if (_currentGameMode == GameMode.CatchGame)
        {
            if (!_gameRunning || !_isLightOn) return;

            bool isLeftPad = (row == 0 && col == 0);
            float targetX = isLeftPad ? -150f : 150f;
            int playerLane = isLeftPad ? 0 : 1;

            if (playerCharacter != null)
            {
                RectTransform rt = playerCharacter.GetComponent<RectTransform>();
                if (rt != null) rt.anchoredPosition = new Vector2(targetX, rt.anchoredPosition.y);
            }
            if (gc != null && gc.playerCharacter != null)
            {
                RectTransform gcRt = gc.playerCharacter.GetComponent<RectTransform>();
                if (gcRt != null) gcRt.anchoredPosition = new Vector2(targetX, gcRt.anchoredPosition.y);
            }

            float reaction = Time.time - _lightOnTime;

            if (_isDualDropMode)
            {
                var activeItemOnThisLane = _activeFallingItems.FirstOrDefault(item => item.row == playerLane && !item.isSettled);
                if (activeItemOnThisLane.uiObject != null)
                {
                    float posY = activeItemOnThisLane.rect != null ? activeItemOnThisLane.rect.anchoredPosition.y : 0f;

                    if (posY <= 0f)
                    {
                        int index = _activeFallingItems.FindIndex(item => item.row == playerLane && !item.isSettled);
                        if (index != -1)
                        {
                            var item = _activeFallingItems[index];
                            item.isSettled = true;
                            _activeFallingItems[index] = item;

                            HandleItemSettlement(item, true, gc);
                            if (item.uiObject != null) Destroy(item.uiObject);
                        }
                    }
                }
            }
            else
            {
                bool isItemOnLeft = (_catchCurrentRow == 0);

                if (_isCurrentItemGood)
                {
                    if (isLeftPad == isItemOnLeft)
                    {
                        gc?.AddScore(1);
                        PlaySound(clipSuccess);
                        _whizToys?.WriteSignals(new List<WhizToysSendModel> { CreateLightCommand(row, col, 25) });
                        _isLightOn = false;
                        WriteLog("true", reaction);
                    }
                    else
                    {
                        ShowErrorFeedback(row, col);
                    }
                }
                else
                {
                    if (isLeftPad == isItemOnLeft)
                    {
                        gc?.AddScore(-1);
                        PlaySound(clipError);
                        _whizToys?.WriteSignals(new List<WhizToysSendModel> { CreateLightCommand(row, col, 1) });
                        _isLightOn = false;
                        WriteLog("bomb_hit", reaction);
                    }
                    else
                    {
                        _whizToys?.WriteSignals(new List<WhizToysSendModel> { CreateLightCommand(row, col, 25) });
                        _isLightOn = false;
                        WriteLog("bomb_dodged_active", reaction);
                    }
                }
            }
        }
    }

    private bool CreateLogFile(GameMode mode)
    {
        try
        {
            string gameName = mode switch
            {
                GameMode.LightningReaction => "Flash",
                GameMode.FastTap => "FastStep",
                GameMode.SingleColor => "Go-No-Go",
                GameMode.CatchGame => "CatchGame",
                GameMode.WhackAMole => "WhackAMole",
                _ => mode.ToString()
            };
            string timeStamp = DateTime.Now.ToString("yyyy-MM-dd_HHmm");
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            string baseName = $"GameLog_{gameName}_{timeStamp}";
            _currentLogFilePath = Path.Combine(path, baseName + ".csv");
            int counter = 1;
            while (File.Exists(_currentLogFilePath)) { _currentLogFilePath = Path.Combine(path, $"{baseName}_{counter}.csv"); counter++; }
            File.WriteAllText(_currentLogFilePath, "GameMode,Result,ReactionTime\n", System.Text.Encoding.UTF8);
            return true;
        }
        catch (Exception ex) { Debug.LogError($"[Log] 建立失敗: {ex.Message}"); return false; }
    }

    private void WriteLog(string result, float time)
    {
        if (string.IsNullOrEmpty(_currentLogFilePath)) return;
        try { string line = $"{_currentGameMode},{result},{time:F2}\n"; File.AppendAllText(_currentLogFilePath, line, System.Text.Encoding.UTF8); } catch { }
    }

    private void LightUpRandomPad()
    {
        if (ConnectedDeviceMap == null || ConnectedDeviceMap.Layout.Row == 0) return;
        _targetRow = UnityEngine.Random.Range(0, ConnectedDeviceMap.Layout.Row);
        _targetCol = UnityEngine.Random.Range(0, ConnectedDeviceMap.Layout.Column);
        _whizToys?.WriteSignals(new List<WhizToysSendModel> { CreateLightCommand(_targetRow, _targetCol, 41) });
        _isLightOn = true; _lightOnTime = Time.time;
    }

    private void TurnOffAllLights()
    {
        if (_whizToys == null || ConnectedDeviceMap == null) return;
        var allOff = new List<WhizToysSendModel>();
        for (int i = 0; i < ConnectedDeviceMap.Layout.Row; i++)
            for (int j = 0; j < ConnectedDeviceMap.Layout.Column; j++)
                if (ConnectedDeviceMap.Blocks[i, j].Active) allOff.Add(CreateLightCommand(i, j, 0));
        if (allOff.Count > 0) _whizToys.WriteSignals(allOff);

        _isLightOn = false;
        var gc = FindFirstObjectByType<GameController>();
        gc?.ResetAllWhackAMoleGridUI();
    }

    private void ShowErrorFeedback(int r, int c)
    {
        _whizToys?.WriteSignals(new List<WhizToysSendModel> { CreateLightCommand(r, c, 1) });
        var gc = FindFirstObjectByType<GameController>();
        if (gc != null) gc.HighlightWhackAMoleGridUI(r, c, gc.colorError);
        StartCoroutine(Routine_TurnOffSinglePad(r, c, 0.3f));
    }

    private IEnumerator Routine_TurnOffSinglePad(int r, int c, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (_currentGameMode == GameMode.CatchGame && !_isDualDropMode && r == _catchCurrentRow && c == _catchCurrentCol) yield break;

        _whizToys?.WriteSignals(new List<WhizToysSendModel> { CreateLightCommand(r, c, 0) });
        var gc = FindFirstObjectByType<GameController>();
        if (gc != null) gc.HighlightWhackAMoleGridUI(r, c, gc.colorDefault);
    }

    private void EndGameCleanup(GameController gc)
    {
        if (gc != null) gc.EndGame();
        _gameRunning = false;
        var allOn = new List<WhizToysSendModel>();
        if (ConnectedDeviceMap != null)
        {
            for (int i = 0; i < ConnectedDeviceMap.Layout.Row; i++)
                for (int j = 0; j < ConnectedDeviceMap.Layout.Column; j++)
                    if (ConnectedDeviceMap.Blocks[i, j].Active) allOn.Add(CreateLightCommand(i, j, 41));
            _whizToys?.WriteSignals(allOn);
        }
        Invoke(nameof(TurnOffAllLights), 2f);
    }

    private WhizToysSendModel CreateLightCommand(int r, int c, int colorIdx)
    {
        return new WhizToysSendModel { Layout = new WhizToysLayout { Row = r, Column = c }, ColorIndex = colorIdx };
    }

    private List<Vector2Int> GetAllActivePads()
    {
        var list = new List<Vector2Int>();
        if (ConnectedDeviceMap == null) return list;
        for (int r = 0; r < ConnectedDeviceMap.Layout.Row; r++)
            for (int c = 0; c < ConnectedDeviceMap.Layout.Column; c++)
                if (ConnectedDeviceMap.Blocks[r, c].Active) list.Add(new Vector2Int(r, c));
        return list;
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            float volume = 1.0f;
            if (clip == clipError) volume = 5.0f;
            audioSource.PlayOneShot(clip, volume);
        }
    }

    private void OnDestroy() => _whizToys?.Stop();
    private void OnApplicationQuit()
    {
        TurnOffAllLights();
        _whizToys?.Stop();
    }
}