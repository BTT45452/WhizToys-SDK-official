using System.Collections;
using System.Collections.Generic;
using Plugins.WhizToys;
using Plugins.WhizToys.Models;
using UnityEngine;
using UnityEngine.UI;

public class RotateDemo : MonoBehaviour
{
    [Header("Basic")] 
    public bool isMobile = false;
    
    [Header("Scan")] public GameObject scanPage;
    public Transform scanPageParent;
    public ScanItem scanItemObject;

    [Header("Main")] public GameObject mainPage;
    public Transform mainPageParent;
    public PressBlock pressBlockObject;
    public PressBlock emptyBlockObject;

    [Header("UI")] public GridLayoutGroup gridLayoutGroup;
    public PressBlock[] showPressBlocks;
    public Button[] rotateButtons;

    // Rotate
    private List<WhizToysLayout> rotatePositions;
    private PressBlock[,] _pressBlocks;
    private int _rotateType = -1;

    private WhizToys _whizToys;
    private WhizToysMap _whizToysMap;
    private bool _scanning = false;

    // Start is called before the first frame update
    void Start()
    {
        if (isMobile)
            _whizToys = new WhizToys_Mobile();
        else
            _whizToys = new WhizToys_Windows();
        
        _whizToys.Initialize();
        _whizToys.OnInitSuccess = () => { print("初始化成功"); };

        _whizToys.OnScanDevice = OnScanDevice;      
        _whizToys.OnScanEnd = OnScanEnd;

        _whizToys.OnConnected = OnConnected;
        _whizToys.OnDisconnect = () => { print("連接斷掉"); };

        _whizToys.OnReceiveSignal = OnReceiveSignal;

        rotatePositions = new List<WhizToysLayout>();

        for (int i = 0; i < 4; i++)
        {
            WhizToysLayout layout = new WhizToysLayout();
            layout.Column = 0;
            layout.Row = 0;
            rotatePositions.Add(layout);
        }
    }

    #region Scan

    public void Scan()
    {
        _whizToys.StartScan(3);
        _scanning = true;
        scanPageParent.DetachChildren();
    }

    private void OnScanDevice(string address, string deviceName)
    {
        ScanItem scanItem = Instantiate(scanItemObject, scanPageParent);
        scanItem.addressText.text = address;
        scanItem.nameText.text = deviceName;
        scanItem.button.onClick.AddListener(() => { Connect(address); });
    }

    private void OnScanEnd()
    {
        _scanning = false;
    }

    #endregion

    #region Rotate

    public void SelectBlock(int row, int column)
    {
        // 確認是否會超出
        if (CheckBorder(row, column))
            return;

        // UpInit
        for (int i = 0; i < showPressBlocks.Length; i++)
        {
            showPressBlocks[i].SetColor(Color.black);
            showPressBlocks[i].pressText.text = "無";
        }
        
        // DownInit
        for (int i = 0; i < _pressBlocks.GetLength(0); i++)
        for (int j = 0; j < _pressBlocks.GetLength(1); j++)
            _pressBlocks[i, j].SetColor(Color.black);

        SetRotatePositions(row, column);

        for (int i = 0; i < rotatePositions.Count; i++)
        {
            WhizToysLayout layout = rotatePositions[i];
            _pressBlocks[layout.Row, layout.Column].SetColor(Color.red);
        }
    }

    public void SelectRotateType(int index)
    {
        if (index == _rotateType)
            return;

        _rotateType = index;

        for (int i = 0; i < rotateButtons.Length; i++)
            rotateButtons[i].interactable = true;

        rotateButtons[index].interactable = false;

        // 設定到自動四個角
        switch (_rotateType)
        {
            case 0:
                SelectBlock(0, 0);
                break;
            case 1:
                SelectBlock(0, _whizToysMap.Layout.Column - 1);
                break;
            case 2:
                SelectBlock(_whizToysMap.Layout.Row - 1, _whizToysMap.Layout.Column - 1);
                break;
            case 3:
                SelectBlock(_whizToysMap.Layout.Row - 1, 0);
                break;
        }
    }

    private void SetRotatePositions(int row, int column)
    {
        rotatePositions = new List<WhizToysLayout>();
        rotatePositions.Add(new WhizToysLayout(row, column));

        // 要注意順序
        switch (_rotateType)
        {
            case 0:
                rotatePositions.Add(new WhizToysLayout(row, column + 1));
                rotatePositions.Add(new WhizToysLayout(row + 1, column));
                rotatePositions.Add(new WhizToysLayout(row + 1, column + 1));
                break;
            case 1:
                rotatePositions.Add(new WhizToysLayout(row + 1, column));
                rotatePositions.Add(new WhizToysLayout(row, column - 1));
                rotatePositions.Add(new WhizToysLayout(row + 1, column - 1));
                break;
            case 2:
                rotatePositions.Add(new WhizToysLayout(row, column - 1));
                rotatePositions.Add(new WhizToysLayout(row - 1, column));
                rotatePositions.Add(new WhizToysLayout(row - 1, column - 1));
                break;
            case 3:
                rotatePositions.Add(new WhizToysLayout(row - 1, column));
                rotatePositions.Add(new WhizToysLayout(row, column + 1));
                rotatePositions.Add(new WhizToysLayout(row - 1, column + 1));
                break;
        }
    }

    private bool CheckBorder(int row, int column)
    {
        int totalRow = _whizToysMap.Layout.Row - 1;
        int totalColumn = _whizToysMap.Layout.Column - 1;

        switch (_rotateType)
        {
            case 0:
                if (row == totalRow)
                    return true;
                if (column == totalColumn)
                    return true;
                break;
            case 1:
                if (row == totalRow)
                    return true;
                if (column == 0)
                    return true;
                break;
            case 2:
                if (row == 0)
                    return true;
                if (column == 0)
                    return true;
                break;
            case 3:
                if (row == 0)
                    return true;
                if (column == totalColumn)
                    return true;
                break;
        }

        return false;
    }

    private int[] RotatePressures(int[] pressures)
    {
        int[] result = new int[4];

        switch (_rotateType)
        {
            case 1:
                result[0] = pressures[3];
                result[1] = pressures[0];
                result[2] = pressures[1];
                result[3] = pressures[2];
                return result;
            case 2:
                result[0] = pressures[2];
                result[1] = pressures[3];
                result[2] = pressures[0];
                result[3] = pressures[1];
                return result;
            case 3:
                result[0] = pressures[1];
                result[1] = pressures[2];
                result[2] = pressures[3];
                result[3] = pressures[0];
                return result;
        }

        return pressures;
    }

    #endregion

    public void Connect(string address)
    {
        if (_scanning)
            return;

        _whizToys.Connect(address);
    }

    private void OnConnected(WhizToysMap whizToysMap)
    {
        CreateMap(whizToysMap);

        scanPage.SetActive(false);
        mainPage.SetActive(true);
        SelectRotateType(0);
    }

    private void CreateMap(WhizToysMap whizToysMap)
    {
        _whizToysMap = whizToysMap;

        for (int i = 0; i < mainPageParent.childCount; i++)
            Destroy(mainPageParent.GetChild(i).gameObject);

        int row = _whizToysMap.Layout.Row;
        int column = _whizToysMap.Layout.Column;

        gridLayoutGroup.constraintCount = column;
        _pressBlocks = new PressBlock[row, column];

        for (int i = 0; i < row; i++)
        {
            for (int j = 0; j < column; j++)
            {
                PressBlock createObject = pressBlockObject;

                if (!_whizToysMap.Blocks[i, j].Active)
                    createObject = emptyBlockObject;

                var x = i;
                var y = j;

                PressBlock pressBlock = Instantiate(createObject, mainPageParent);
                _pressBlocks[i, j] = pressBlock;
                _pressBlocks[i, j].pressText.text = x + "," + j;
                _pressBlocks[i, j].ButtonAction = () => { SelectBlock(x, y); };
            }
        }
    }

    private void OnReceiveSignal(List<WhizToysSignal> values)
    {
        if (rotatePositions.Count == 0)
            return;

        for (int i = 0; i < values.Count; i++)
        {
            WhizToysLayout layout = values[i].Layout;

            for (int j = 0; j < rotatePositions.Count; j++)
            {
                if (layout.Compare(rotatePositions[j]))
                {
                    int[] pressures = values[i].Pressures;
                    pressures = RotatePressures(pressures);

                    PressBlock showPressBlock = showPressBlocks[j];
                    for (int k = 0; k < pressures.Length; k++)
                        showPressBlock.blocks[k].color = ConvertColor(pressures[k]);
                    
                    WhizToysBlock block = _whizToysMap.Blocks[layout.Row, layout.Column];
                    if (block.AllPressure)
                    {
                        showPressBlock.pressText.text = "全";
                        continue;
                    }
                    
                    if (block.IsLeft)
                    {
                        showPressBlock.pressText.text = GetLeft();
                        continue;
                    }

                    if (block.IsRight)
                    {
                        showPressBlock.pressText.text = GetRight();
                        continue;
                    }
                    
                    if (block.IsUp)
                    {
                        showPressBlock.pressText.text = GetUp();
                        continue;
                    }
                    
                    if (block.IsDown)
                    {
                        showPressBlock.pressText.text = GetDown();
                    }
                }
            }
        }
    }

    private string GetUp()
    {
        string result = "上";
        
        switch (_rotateType)
        {
            case 1:
                result = "左";
                break;
            case 2:
                result = "下";
                break;
            case 3:
                result = "右";
                break;
        }
        
        return result;
    }
    
    private string GetDown()
    {
        string result = "下";
        
        switch (_rotateType)
        {
            case 1:
                result = "右";
                break;
            case 2:
                result = "上";
                break;
            case 3:
                result = "左";
                break;
        }
        
        return result;
    }
    
    private string GetLeft()
    {
        string result = "左";
        
        switch (_rotateType)
        {
            case 1:
                result = "下";
                break;
            case 2:
                result = "右";
                break;
            case 3:
                result = "上";
                break;
        }
        
        return result;
    }
    
    private string GetRight()
    {
        string result = "右";
        
        switch (_rotateType)
        {
            case 1:
                result = "上";
                break;
            case 2:
                result = "左";
                break;
            case 3:
                result = "下";
                break;
        }
        
        return result;
    }

    private Color ConvertColor(int index)
    {
        Color color = Color.black;

        switch (index)
        {
            case 1:
                color = Color.green;
                break;
            case 2:
                color = Color.blue;
                break;
            case 3:
                color = Color.red;
                break;
        }

        return color;
    }
}