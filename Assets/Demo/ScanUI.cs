using UnityEngine;
using UnityEngine.UI;

public class ScanUI : MonoBehaviour
{
    [Header("UI 引用 (請在 Inspector 拖曳)")]
    public Button scanButton;         // 您的 "ScanButton"
    public Transform listParent;      // 您 ScrollView 裡的 "Content" 物件
    public GameObject scanItemPrefab; // 您在 Project 裡的 "ScanItem" 預製件

    private void Start()
    {
        if (scanButton != null)
        {
            // 讓 Scan 按鈕按下時，呼叫下面的 OnScanClicked 函式
            scanButton.onClick.AddListener(OnScanClicked);
        }
        ClearList(); // 一開始先清空列表
    }

    // 按下 "Scan" 按鈕時
    private void OnScanClicked()
    {
        print("ScanUI: Scan 按鈕被按下");
        ClearList(); // 清空舊列表
        
        // 呼叫「常駐」的 Demo.Instance 開始掃描
        if (Demo.Instance != null)
        {
            Demo.Instance.StartScan(); // <--- 呼叫 Demo.cs 的掃描功能
        }
        else { Debug.LogError("找不到 Demo.Instance！");}
    }

    // 這個函式會由 Demo.cs 的 OnScanDevice 呼叫
    public void AddDeviceToList(string address, string deviceName)
    {
        print($"ScanUI: 收到裝置 {deviceName}，準備產生列表項");
        if (scanItemPrefab == null || listParent == null) return;

        // 在 "Content" (listParent) 底下產生一個新的列表項
        GameObject itemGO = Instantiate(scanItemPrefab, listParent);
        
        ScanItem itemScript = itemGO.GetComponent<ScanItem>();
        if (itemScript != null)
        {
            // 設定顯示的文字
            if(itemScript.addressText) itemScript.addressText.text = address;
            if(itemScript.nameText) itemScript.nameText.text = deviceName;

            // 幫這個新按鈕加上點擊事件監聽
            if(itemScript.button)
            {
                // 移除舊的監聽，避免重複綁定 (雖然 Instantiate 不太會)
                itemScript.button.onClick.RemoveAllListeners(); 
                // 當這個列表項被點擊時，呼叫下面的 OnDeviceClicked 函式
                itemScript.button.onClick.AddListener(() => 
                {
                    OnDeviceClicked(address); 
                });
            }
        }
    }
    
    // 當列表中的某個裝置被點擊時
    private void OnDeviceClicked(string address)
    {
        print($"ScanUI: 裝置 {address} 被點擊，請求連線");
        // 呼叫「常駐」的 Demo.Instance 開始連線
        if (Demo.Instance != null)
        {
            Demo.Instance.Connect(address); // <--- 呼叫 Demo.cs 的連線功能
        }
        else { Debug.LogError("找不到 Demo.Instance！");}
    }


    // 清空列表
    private void ClearList()
    {
        if (listParent == null) return;
        foreach (Transform child in listParent)
        {
            Destroy(child.gameObject);
        }
    }
}