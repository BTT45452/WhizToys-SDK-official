using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Plugins.WhizToys;
using Plugins.WhizToys.Models;

public class WhizToys_Mobile : WhizToys
{
    // Init
    public override void Initialize()
    {
        BluetoothLEHardwareInterface.Initialize(true, false,
            () => { OnInitSuccess?.Invoke(); },
            (error) =>
            {
                if (error.Contains("Bluetooth LE Not Enabled"))
                    BluetoothLEHardwareInterface.BluetoothEnable(true);
            });
    }

    // Scan
    public override async void StartScan(float seconds)
    {
        await ScanAsync(seconds);
    }

    private async Task ScanAsync(float seconds)
    {
        List<string> addresses = new List<string>();

        BluetoothLEHardwareInterface.ScanForPeripheralsWithServices(null, null,
            (address, deviceName, rssi, bytes) =>
            {
                if (!addresses.Contains(address))
                    OnScanDevice?.Invoke(address, deviceName);

                addresses.Add(address);
            },
            false);

        await Task.Delay(TimeSpan.FromSeconds(seconds));
        BluetoothLEHardwareInterface.StopScan();
        OnScanEnd?.Invoke();
    }

    // Connect
    public override void Connect(string address)
    {
        bool layoutFlag = false;
        bool sensorFlag = false;
        bool lightsFlag = false;

        BluetoothLEHardwareInterface.ConnectToPeripheral(address, null, null,
            (address, serviceUUID, characteristicUUID) =>
            {
                // CheckDevice
                if (serviceUUID == _serviceUUID)
                {
                    if (characteristicUUID == _layoutUUID)
                        layoutFlag = true;

                    if (characteristicUUID == _sensorUUID)
                        sensorFlag = true;

                    if (characteristicUUID == _lightsUUID)
                        lightsFlag = true;

                    if (layoutFlag && sensorFlag && lightsFlag)
                    {
                        connectedDeviceAddress = address;
                        Subscribe();
                    }
                }
            }, (error) =>
            {
                connectedDeviceAddress = null;
                OnDisconnect?.Invoke();
            });
    }

    private async void Subscribe()
    {
        await SubscribeSensor();
        WhizToysMap result = await GetMap();

        AllPressures = new int[result.Layout.Row, result.Layout.Column][];

        for (int i = 0; i < result.Layout.Row; i++)
        for (int j = 0; j < result.Layout.Column; j++)
            AllPressures[i, j] = new int[4];

        _originMap = result;
        OnConnected?.Invoke(result);
    }

    private Task<bool> SubscribeSensor()
    {
        var tcs = new TaskCompletionSource<bool>();

        BluetoothLEHardwareInterface.SubscribeCharacteristicWithDeviceAddress(connectedDeviceAddress,
            _serviceUUID, _sensorUUID, (notifyAddress, notifyCharacteristic) => { tcs.TrySetResult(true); },
            (address, characteristicUUID, bytes) => { ProcessPressSignal(bytes); });

        return tcs.Task;
    }

    private Task<WhizToysMap> GetMap()
    {
        var tcs = new TaskCompletionSource<WhizToysMap>();

        BluetoothLEHardwareInterface.ReadCharacteristic(connectedDeviceAddress, _serviceUUID, _layoutUUID,
            (characteristic, bytes) =>
            {
                int[] result = ConvertLayout(bytes[0]);

                WhizToysMap map = new WhizToysMap();

                map.Layout = new WhizToysLayout();
                map.Layout.Row = result[0];
                map.Layout.Column = result[1];

                WhizToysBlock[,] blocks = new WhizToysBlock[map.Layout.Row, map.Layout.Column];

                bool[,] status = ConvertMap(map.Layout.Row, map.Layout.Column, bytes);

                int index = 1;
                _rotateMap = new Dictionary<int, (int, int)>();

                for (int i = 0; i < map.Layout.Row; i++)
                for (int j = 0; j < map.Layout.Column; j++)
                {
                    WhizToysBlock block = new WhizToysBlock(this, i, j, status[i, j]);
                    blocks[i, j] = block;
                    _rotateMap.Add(index, (i, j));

                    index++;
                }

                map.Blocks = blocks;
                tcs.TrySetResult(map);
            });

        return tcs.Task;
    }

    // WriteSignal
    public override async void WriteSignals(List<WhizToysSendModel> sendModels, int delayTime = 200)
    {
        List<List<WhizToysSendModel>> splitList = SplitList(sendModels, 6);

        for (int i = 0; i < splitList.Count; i++)
        {
            List<WhizToysSendModel> data = splitList[i];
            int dataCount = data.Count;

            // 一份可以傳6份
            byte[] result = new byte[dataCount * 3 + 1];
            result[0] = WriteHeader(dataCount * 3);

            for (int j = 0; j < dataCount; j++)
            {
                WhizToysSendModel sendModel = data[j];

                int tab = j * 3;
                int colorIndex = sendModel.ColorIndex;
                int row = sendModel.Layout.Row;
                int column = sendModel.Layout.Column;

                result[tab + 1] = WriteLayout(row, column);
                result[tab + 2] = WriteControl(
                    lightMode: (int)sendModel.LightMode,
                    clickMode: (int)sendModel.CommandMode,
                    clickShowMode: (int)sendModel.FeedBackMode,
                    showMode: (int)sendModel.ShowTimeMode
                );
                result[tab + 3] = WriteColor(colorIndex);
            }

            BluetoothLEHardwareInterface.WriteCharacteristic(
                connectedDeviceAddress,
                _serviceUUID,
                _lightsUUID,
                result,
                dataCount * 3 + 1,
                false,
                (characteristicUUID) => { BluetoothLEHardwareInterface.Log("Write Succeeded"); }
            );

            // 這邊等待 0.2秒
            await Task.Delay(delayTime);
        }
    }

    public override void Stop()
    {
    }
}