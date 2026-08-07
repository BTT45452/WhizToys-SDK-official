using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Plugins.WhizToys.Models;
using UnityEngine;

namespace Plugins.WhizToys
{
    public abstract class WhizToys
    {
        // private string _deviceAddress = "D4:8D:3F:E0:1B:D9";
        protected string connectedDeviceAddress = null;

#if UNITY_ANDROID
        protected readonly string _serviceUUID = "0000fee0-0000-1000-8000-00805f9b34fb";
        protected readonly string _layoutUUID = "0000fee1-0000-1000-8000-00805f9b34fb";
        protected readonly string _sensorUUID = "0000fee2-0000-1000-8000-00805f9b34fb";
        protected readonly string _lightsUUID = "0000fee3-0000-1000-8000-00805f9b34fb";
#else
        protected readonly string _serviceUUID = "FEE0";
        protected readonly string _layoutUUID = "FEE1";
        protected readonly string _sensorUUID = "FEE2";
        protected readonly string _lightsUUID = "FEE3";
#endif

        // Events
        public Action OnInitSuccess;

        public Action<string, string> OnScanDevice;
        public Action OnScanEnd;

        public Action<WhizToysMap> OnConnected;
        public Action OnDisconnect;
        public Action<List<WhizToysSignal>> OnReceiveSignal;

        // Var
        public int[,][] AllPressures;
        protected WhizToysMap _originMap;
        protected Dictionary<int, (int, int)> _rotateMap;

        #region Init

        public abstract void Initialize();

        #endregion

        #region Scan

        public abstract void StartScan(float seconds);

        #endregion

        #region Connect

        public abstract void Connect(string address);

        protected void ProcessPressSignal(byte[] values)
        {
            List<WhizToysSignal> signals = new List<WhizToysSignal>();

            for (int i = 0; i < values.Length; i += 2)
            {
                WhizToysSignal signal = new WhizToysSignal();

                int[] layout = ConvertLayout(values[i]);
                signal.Layout.Row = layout[0];
                signal.Layout.Column = layout[1];
                signal.Pressures = ConvertPressure(values[i + 1]);

                AllPressures[signal.Layout.Row, signal.Layout.Column] = signal.Pressures;
                signals.Add(signal);
            }
            
            OnReceiveSignal?.Invoke(signals);
        }

        #endregion

        #region Write

        public abstract void WriteSignals(List<WhizToysSendModel> sendModels, int delayTime = 200);

        protected List<List<WhizToysSendModel>> SplitList(List<WhizToysSendModel> source, int chunkSize)
        {
            List<List<WhizToysSendModel>> result = new List<List<WhizToysSendModel>>();
            for (int i = 0; i < source.Count; i += chunkSize)
            {
                int currentChunkSize = Math.Min(chunkSize, source.Count - i);
                List<WhizToysSendModel> chunk = source.GetRange(i, currentChunkSize);
                result.Add(chunk);
            }

            return result;
        }

        protected byte WriteHeader(int count)
        {
            return (byte)count;
        }

        protected byte WriteLayout(int row, int column)
        {
            return (byte)((row << 4) | column);
        }

        protected byte WriteControl(int lightMode, int clickMode, int clickShowMode, int showMode)
        {
            byte lightModeResult = (byte)(lightMode << 5);
            byte clickModeResult = (byte)(clickMode << 4);
            byte clickShowModeResult = (byte)(clickShowMode << 1);
            byte showModeResult = (byte)showMode;

            return (byte)(lightModeResult | clickModeResult | clickShowModeResult | showModeResult);
        }

        protected byte WriteColor(int colorIndex)
        {
            return (byte)Math.Clamp(colorIndex, 0, 61);
        }

        #endregion

        #region Util

        protected bool[,] ConvertMap(int row, int column, byte[] bytes)
        {
            bool[,] result = new bool[row, column];

            List<string> binarys = new List<string>();
            for (int i = 1; i < bytes.Length; i++)
            {
                string binary = ConvertBinary(bytes[i]);

                for (int j = 0; j < binary.Length; j += 2)
                    binarys.Add(binary.Substring(j, 2));
            }

            for (int i = 0; i < row; i++)
            {
                for (int j = 0; j < column; j++)
                {
                    int index = i * column * 4 + j * 2;
                    string binary = binarys[index];

                    result[i, j] = !binary.Equals("00");
                }
            }

            return result;
        }

        protected int[] ConvertLayout(byte value)
        {
            int row = (value >> 4) & 0x0F;
            int column = value & 0x0F;

            return new[] { row, column };
        }

        protected int[] ConvertPressure(byte value)
        {
            int[] pressures = new int[4];

            pressures[0] = (value >> 6) & 0x03;
            pressures[1] = (value >> 4) & 0x03;
            pressures[2] = (value >> 2) & 0x03;
            pressures[3] = value & 0x03;

            return pressures;
        }

        protected string ConvertBinary(byte value)
        {
            return Convert.ToString(value, 2).PadLeft(8, '0');
        }

        #endregion

        public abstract void Stop();
    }
}