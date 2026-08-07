using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Plugins.WhizToys.Models;
using UnityEngine;
using wclBluetooth;
using wclCommon;

namespace Plugins.WhizToys
{
    public class WhizToys_Windows : WhizToys
    {
        private wclBluetoothManager _fManager;
        private wclGattClient _fClient;
        private wclBluetoothRadio _radio;
        private wclGattCharacteristic _writeCharacteristic;

        private bool _isInit = false;
        private bool _isScanning = false;

        Thread _readingThread;
        private bool _isConnecting = true;
        private bool _isReading = false;

        private static SynchronizationContext _unityContext;

        // Init
        public override void Initialize()
        {
            if (_unityContext == null)
                _unityContext = SynchronizationContext.Current;

            _isInit = false;

            _fManager = new wclBluetoothManager();
            _fManager.AfterOpen += FManager_AfterOpen;
            _fManager.OnDiscoveringStarted += FManager_OnDiscoveringStarted;
            _fManager.OnDeviceFound += FManager_OnDeviceFound;
            _fManager.OnDiscoveringCompleted += FManager_OnDiscoveringCompleted;

            _fClient = new wclGattClient();
            _fClient.OnConnect += FClient_OnConnect;
            _fClient.OnCharacteristicChanged += FClient_OnCharacteristicChanged;

            Int32 res = _fManager.Open();
            if (res != wclErrors.WCL_E_SUCCESS)
            {
                Debug.Log("Unable to open Bluetooth Manager: 0x" + res.ToString("X8"));
                return;
            }

            OnInitSuccess?.Invoke();
        }

        private void FManager_AfterOpen(object sender, EventArgs e)
        {
            Int32 res = _fManager.GetLeRadio(out _radio);
            if (res != wclErrors.WCL_E_SUCCESS)
                Debug.Log("Unable to get working Bluetooth LE radio: 0x" + res.ToString("X8"));
        }

        // Scan
        public override void StartScan(float seconds)
        {
            if (_isScanning)
                return;

            Int32 res = _radio.Discover((byte)seconds, wclBluetoothDiscoverKind.dkBle);
            if (res != wclErrors.WCL_E_SUCCESS)
                Debug.Log("Unable to start discovering: 0x" + res.ToString("X8"));
        }

        private void FManager_OnDiscoveringStarted(object Sender, wclBluetoothRadio Radio)
        {
            _isScanning = true;
        }

        private void FManager_OnDeviceFound(object Sender, wclBluetoothRadio Radio, Int64 Address)
        {
            List<Int64> addresses = new List<Int64>();

            if (!addresses.Contains(Address))
            {
                addresses.Add(Address);

                Int32 res = Radio.GetRemoteName(Address, out var name);

                if (res != wclErrors.WCL_E_SUCCESS)
                    name = "UNKNOWN";

                OnScanDevice?.Invoke(Address.ToString("X12"), name);
            }
        }

        private void FManager_OnDiscoveringCompleted(object Sender, wclBluetoothRadio Radio, Int32 Error)
        {
            _isScanning = false;
            OnScanEnd?.Invoke();
        }

        // Connect

        public override void Connect(string address)
        {
            _fClient.Address = Convert.ToInt64(address, 16);
            Int32 res = _fClient.Connect(_radio);
            if (res != wclErrors.WCL_E_SUCCESS)
                Debug.Log("Connect to selected device failed: 0x" + res.ToString("X8"));
        }

        private void FClient_OnConnect(object Sender, Int32 Error)
        {
            if (Error != wclErrors.WCL_E_SUCCESS)
            {
                Debug.Log("Connect failed: 0x" + Error.ToString("X8"));
                return;
            }

            Int32 res = _fClient.ReadServices(wclGattOperationFlag.goNone, out var services);

            if (res != wclErrors.WCL_E_SUCCESS)
            {
                Debug.Log("Read services failed: 0x" + res.ToString("X8"));
                return;
            }

            if (services == null || services.Length == 0)
            {
                Debug.Log("No services found");
                return;
            }

            WhizToysMap map = null;

            foreach (wclGattService service in services)
            {
                String uuid;
                if (service.Uuid.IsShortUuid)
                    uuid = service.Uuid.ShortUuid.ToString("X4");
                else
                    uuid = service.Uuid.LongUuid.ToString();

                if (uuid == _serviceUUID)
                {
                    wclGattCharacteristic[] characteristics;
                    res = _fClient.ReadCharacteristics(service, wclGattOperationFlag.goNone, out characteristics);
                    if (res != wclErrors.WCL_E_SUCCESS)
                    {
                        Debug.Log("Read characteristics failed: 0x" + res.ToString("X8"));
                        return;
                    }

                    if (characteristics == null || characteristics.Length == 0)
                    {
                        Debug.Log("Characteristics not found");
                        return;
                    }

                    foreach (wclGattCharacteristic characteristic in characteristics)
                    {
                        if (characteristic.Uuid.IsShortUuid)
                            uuid = characteristic.Uuid.ShortUuid.ToString("X4");
                        else
                            uuid = characteristic.Uuid.LongUuid.ToString();

                        if (uuid == _layoutUUID)
                        {
                            Byte[] value;
                            res = _fClient.ReadCharacteristicValue(characteristic, wclGattOperationFlag.goNone,
                                out value);

                            if (res != wclErrors.WCL_E_SUCCESS)
                            {
                                Debug.Log("Read value failed: 0x" + res.ToString("X8"));
                                return;
                            }

                            int[] result = ConvertLayout(value[0]);

                            map = new WhizToysMap();
                            map.Layout = new WhizToysLayout();
                            map.Layout.Row = result[0];
                            map.Layout.Column = result[1];

                            WhizToysBlock[,] blocks = new WhizToysBlock[map.Layout.Row, map.Layout.Column];

                            bool[,] status = ConvertMap(map.Layout.Row, map.Layout.Column, value);

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

                            _isConnecting = false;
                            AllPressures = new int[map.Layout.Row, map.Layout.Column][];

                            for (int i = 0; i < map.Layout.Row; i++)
                            for (int j = 0; j < map.Layout.Column; j++)
                                AllPressures[i, j] = new int[4];
                        }

                        if (uuid == _sensorUUID)
                        {
                            res = _fClient.SubscribeForNotifications(
                                characteristic,
                                wclGattOperationFlag.goReadFromDevice,
                                wclGattProtectionLevel.plNone,
                                wclGattSubscribeKind.skNotification
                            );
                            if (res != wclErrors.WCL_E_SUCCESS)
                            {
                                Debug.Log("Subscribe failed: 0x" + res.ToString("X8"));
                                return;
                            }
                        }

                        if (uuid == _lightsUUID)
                        {
                            _writeCharacteristic = characteristic;
                        }
                    }

                    break;
                }
            }

            if (map != null)
            {
                _isInit = true;
                OnConnected?.Invoke(map);
            }
        }

        private void FClient_OnCharacteristicChanged(object Sender, UInt16 Handle, Byte[] Value)
        {
            ProcessPressSignal(Value);
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

                _fClient.WriteCharacteristicValue(_writeCharacteristic, result);
                // 這邊等待 0.2秒
                await Task.Delay(delayTime);
            }
        }

        public override void Stop()
        {
            if (_fClient != null)
            {
                Int32 res = _fClient.Disconnect();
                if (res != wclErrors.WCL_E_SUCCESS)
                    Debug.Log("Disconnect failed: 0x" + res.ToString("X8"));
                _fClient = null;
            }

            if (_fManager != null)
            {
                Int32 res = _fManager.Close();
                if (res != wclErrors.WCL_E_SUCCESS)
                    Debug.Log("Close Bluetooth Manager failed: 0x" + res.ToString("X8"));
                _fManager = null;
            }
        }
    }
}