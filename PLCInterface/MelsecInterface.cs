using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using System.Windows;

namespace PLCInterface
{
    class PlcVariable
    {
        public int VarIndex { get; set; } = -1;
        public string VarName { get; set; } = string.Empty;
        public string DeviceAddress { get; set; } = string.Empty;       
        public int ReadValue { get; set; }
        public int ChannelNo { get; set; }

        public PlcVariable(int index, string name, string device, int ch=1)
        {
            VarIndex = index;
            VarName = name;
            DeviceAddress = device;
            ChannelNo = ch; // 멀티채널 지원
        }
    }

    class MelsecInterface : CommonInterface
    {
        public ActUtlTypeLib.ActUtlType aut;
        public int StationNo { get; set; }
        private static int RetryNumLimit { get; set; } = 3;
        public string DeviceRandomToRead { get; set; } = string.Empty;
        public bool IsUIReady { get; set; } = false;

        int[] readValues;

        public string DeviceRandomToWrite { get; set; } = string.Empty;

        List<PlcVariable> ReadDevices;
        List<PlcVariable> WriteDevices;
        //private Task plcInterfaceTask;
        private string PreviousRead { get; set; } = string.Empty;

        public int NumChannel { get; set; } = 1;

        public MelsecInterface()
        {
            try
            {
                NumChannel = Convert.ToInt32(ConfigurationManager.AppSettings["NumChannel"] ?? "1");

                #region Read Configuration

                int interfaceIndex = 0;
                ReadDevices = new List<PlcVariable>();

                int j;
                for (j = 0; j < NumChannel; j++)
                {
                    int modelQty = Convert.ToInt32(ConfigurationManager.AppSettings["ModelQty"] ?? "1");
                    string deviceModel = ConfigurationManager.AppSettings["ModelStartAddress"] ?? string.Empty;
                    if (j > 0)
                    {
                        modelQty = Convert.ToInt32(ConfigurationManager.AppSettings[$"ModelQty{j + 1}"] ?? "1");
                        deviceModel = ConfigurationManager.AppSettings["ModelStartAddress{j + 1}"] ?? string.Empty;
                    }
                    if (modelQty > 1 && deviceModel.Length > 1)
                    {
                        string prefix = Regex.Replace(deviceModel, @"[^a-zA-Z]", "");
                        int number = Convert.ToInt32(Regex.Replace(deviceModel, @"[^0-9]", ""));

                        for (int i = 0; i < modelQty; i++)
                        {
                            var plcVar = new PlcVariable(interfaceIndex++, $"Model{i + 1}", $"{prefix}{number++}", j+1);
                            ReadDevices.Add(plcVar);
                        }
                    }
                }

                for (j = 0; j < NumChannel; j++)
                {
                    CheckPlcAddress(interfaceIndex++, "TriggerAddress", true, j+1);
                    CheckPlcAddress(interfaceIndex++, "MbbTriggerAddress", true, j+1);
                    CheckPlcAddress(interfaceIndex++, "ReadyAddress", true, j+1);
                    CheckPlcAddress(interfaceIndex++, "Word1ModelAddress", true, j + 1);
                    CheckPlcAddress(interfaceIndex++, "Word2ModelAddress", true, j + 1);
                }

                #endregion Read Configuration

                #region Write Configuration

                interfaceIndex = 0;
                WriteDevices = new List<PlcVariable>();

                CheckPlcAddress(interfaceIndex++, "AnomalyOnAddress", false);
                CheckPlcAddress(interfaceIndex++, "AnomalyOffAddress", false);
                CheckPlcAddress(interfaceIndex++, "AliveAddress", false);
                CheckPlcAddress(interfaceIndex++, "CaptureCompleteAddress", false);

                #endregion Write Configuration

                GetDevicesAddressRandom();

                readValues = new int[ReadDevices.Count];

                IsConfigurationSuccess = true;
            }
            catch(Exception ex)
            {
                Logger.Error($"Exception on {System.Reflection.MethodBase.GetCurrentMethod().Name} >>> {ex.Message}\r\n{ex.StackTrace}");
                IsConfigurationSuccess = false;
            }
            finally { }
        }

        private void GetDevicesAddressRandom()
        {
            foreach(PlcVariable item in ReadDevices)
            {
                if (item.VarIndex == 0)
                {
                    DeviceRandomToRead = item.DeviceAddress;
                }
                else
                {
                    DeviceRandomToRead = DeviceRandomToRead + "\n" + item.DeviceAddress;
                }
            }

            foreach (PlcVariable item in WriteDevices)
            {
                if (item.VarIndex == 0)
                {
                    DeviceRandomToWrite = item.DeviceAddress;
                }
                else
                {
                    DeviceRandomToWrite = DeviceRandomToWrite + "\n" + item.DeviceAddress;
                }
            }
        }
        public override string StartInteface()
        {
            string returnMessage = string.Empty;
            try
            {
                aut = new ActUtlTypeLib.ActUtlType();
                if (CheckConnectionInfo())
                {
                    if (OpenConnection() == 0)
                    {
                        IsAlive = true;
                        returnMessage = $"PLC connection is success";
                        Logger.Info(returnMessage);
                    }
                    else
                    {
                        returnMessage = "PLC connection is Failed";
                        Logger.Error(returnMessage);
                    }
                }
                else
                {
                    returnMessage = $"PLC connection infomation is wrong";
                    Logger.Error(returnMessage);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Exception on {System.Reflection.MethodBase.GetCurrentMethod().Name} >>> {ex.Message}\r\n{ex.StackTrace}");
                returnMessage = ex.Message;
            }
            finally { }

            return returnMessage;
        }

        private bool CheckConnectionInfo()
        {
            bool isChecked = false;
            try
            {
                int logicalStationNumber = Convert.ToInt32(ConfigurationManager.AppSettings["MelsecStationNo"] ?? "0");
                if(logicalStationNumber > 0 && logicalStationNumber < 1000)
                {
                    isChecked = true;
                    StationNo = logicalStationNumber;
                    Logger.Info($"The logical station number is proper: [{logicalStationNumber}]");
                }
                else
                {
                    Logger.Warn($"The logical station number is not proper: [{logicalStationNumber}]");
                }
            }
            catch(Exception ex)
            {
                Logger.Error($"Exception on {System.Reflection.MethodBase.GetCurrentMethod().Name} >>> {ex.Message}\r\n{ex.StackTrace}");
            }
            finally { }
            
            return isChecked;
        }
        public override int OpenConnection()
        {
            aut.ActLogicalStationNumber = StationNo;

            dynamic res = aut.Open();
            if (res != 0)
            {
                Thread.Sleep(1000);
                res = aut.Open();
                if (res != 0)
                {
                    Logger.Error($"{System.Reflection.MethodBase.GetCurrentMethod().Name}(): PLC Connection is Failed. err code: 0x{Convert.ToString(Convert.ToInt32(res), 16)}");
                }
                else
                {
                    Logger.Info($"{System.Reflection.MethodBase.GetCurrentMethod().Name}(): PLC Connection is Success (2)");
                }
            }
            else
            {
                Logger.Info($"{System.Reflection.MethodBase.GetCurrentMethod().Name}(): PLC Connection is Success (1)");
            }
            return res;
        }

        public override int CloseConnection()
        {
            dynamic res = aut.Close();
            if (res != 0)
            {
                Thread.Sleep(1000);
                res = aut.Close();
                if (res != 0)
                {
                    Logger.Error($"{System.Reflection.MethodBase.GetCurrentMethod().Name}(): PLC Close Connection is Failed. err code: 0x{Convert.ToString(Convert.ToInt32(res), 16)}");
                }
                else
                {
                    Logger.Info($"{System.Reflection.MethodBase.GetCurrentMethod().Name}(): PLC Close Connection is Success (2)");
                }
            }
            else
            {
                Logger.Info($"{System.Reflection.MethodBase.GetCurrentMethod().Name}(): PLC Close Connection is Success (1)");
            }
            return res;
        }

        public override string ReadPlcValues()
        {
            try
            {
                if (ReadFromPLCRandom(DeviceRandomToRead, ReadDevices.Count, ref readValues) != 0)
                {
                    Logger.Error($"PLC read error on {System.Reflection.MethodBase.GetCurrentMethod().Name}. device:[{DeviceRandomToRead.Replace("\n", "/")}]");
                    return $"PLC Read Error: [{DeviceRandomToRead.Replace("\n", "/")}]";
                }
                else
                {
                    // 비동기로 보낸다
                    int i = 0;
                    foreach(PlcVariable item in ReadDevices)
                    {
                        item.ReadValue = readValues[i++];
                    }
                    string json = JsonConvert.SerializeObject(ReadDevices);
                    //Task.Run(() => HttpMessage.SendHttpMessage(json));
                    var messageTasks = new List<Task>();
                    for (int j = 0; j < NumChannel; j++)
                    {
                        messageTasks.Add(Task.Run(() =>
                        {
                            HttpMessage.SendHttpMessage(json, j + 1);
                        }));
                        Thread.Sleep(10);   // 이 부분 없으면 꼬임(TODO: 쓰레드 간 변수 독립성 보장 필요)
                    }
                    try
                    {
                        Task t = Task.WhenAll(messageTasks);
                        t.Wait();
                    }
                    catch (AggregateException) { }
                    catch (Exception ex)
                    {
                        Logger.Error($"Exception on {System.Reflection.MethodBase.GetCurrentMethod().Name} >>> {ex.Message}\r\n{ex.StackTrace}");
                    }

                    var itemReady = ReadDevices.Find(x => x.VarName == "ReadyAddress");
                    if(itemReady != null && itemReady.ReadValue == 0)
                    {
                        SetAPLCValueOn(itemReady.DeviceAddress);
                    }

                    if(PreviousRead != string.Join(" ", readValues))
                    {
                        PreviousRead = string.Join(" ", readValues);
                        Logger.Trace(PreviousRead);
                    }
                    return $"PLC Read Success [{string.Join(" ", readValues)}]";
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Exception on {System.Reflection.MethodBase.GetCurrentMethod().Name} >>> {ex.Message}\r\n{ex.StackTrace}");
                return ex.Message;
            }
            finally { }
        }
        

        private void CheckPlcAddress(int index, string appSettingName, bool isRead = true, int ch = 1)
        {
            try
            {
                string appSettingNameCH = appSettingName;
                if (ch > 1)
                    appSettingNameCH = $"{appSettingNameCH}{ch}";

                string device = ConfigurationManager.AppSettings[appSettingNameCH] ?? string.Empty;
                if (device != string.Empty)
                {
                    var plcVar = new PlcVariable(index, appSettingName, device, ch);
                    if (isRead)
                    {
                        ReadDevices.Add(plcVar);
                    }
                    else
                    {
                        WriteDevices.Add(plcVar);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Exception on {System.Reflection.MethodBase.GetCurrentMethod().Name} >>> {ex.Message}\r\n{ex.StackTrace}");
            }
            finally { }
        }
        
        public int ReadFromPLCBlock(string device, int size, ref int[] values)
        {
            int res = 0;
            try
            {
                lock (aut)
                {
                    object r = aut.ReadDeviceBlock(device, size, out values[0]);
                    if (Convert.ToInt32(r) != 0)
                    {
                        aut.Close();

                        Thread.Sleep(500);
                        var openRs = aut.Open();
                        r = aut.ReadDeviceBlock(device, size, out values[0]);
                        if (Convert.ToInt32(r) != 0)
                        {
                            Logger.Error($"{System.Reflection.MethodBase.GetCurrentMethod().Name}(): PLC Interface Failed. err code: 0x{Convert.ToString(Convert.ToInt32(r), 16)}");
                            res = -1;
                        }
                    }
                    else
                    {
                        // DoNothing();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Exception on {System.Reflection.MethodBase.GetCurrentMethod().Name} >>> {ex.Message}\r\n{ex.StackTrace}");
                res = -1;
            }
            finally { }

            return res;
        }

        public int ReadFromPLCRandom(string device, int size, ref int[] values)
        {
            int res = 0;
            object obj = new object();

            try
            {
                lock (aut)
                {
                    for (int i = 0; i < RetryNumLimit; i++)
                    {
                        Logger.Debug("Start ReadDevice Random");
                        obj = aut.ReadDeviceRandom(device, size, out values[0]);
                        Logger.Debug("End ReadDevice Random");
                        if (Convert.ToInt32(obj) == 0)
                        {
                            Logger.Debug($"PLC Random Block Read Success - device:{device.Replace("\n", "/")}, size:{size}, value:{values[0]}");
                            return res;
                        }
                        else
                        {
                            Logger.Error($"{System.Reflection.MethodBase.GetCurrentMethod().Name} >>> Retry to Read PLC Random Value. Addr:{device.Replace("\n", "/")}, Melsec errcode:0x{Convert.ToString(Convert.ToInt32(obj), 16)}");
                            Thread.Sleep(10);
                        }
                    }

                    aut.Close();
                    Thread.Sleep(100);
                    var openRs = aut.Open();
                    Logger.Debug("Start2 ReadDevice Random");
                    obj = aut.ReadDeviceRandom(device, size, out values[0]);
                    Logger.Debug("End2 ReadDevice Random");
                    if (Convert.ToInt32(obj) != 0)
                    {
                        Logger.Error($"{System.Reflection.MethodBase.GetCurrentMethod().Name}(): PLC Interface Failed. device:{device.Replace("\n", "/")}, err code: 0x{Convert.ToString(Convert.ToInt32(obj), 16)}");
                        res = -1;
                    }
                    else
                    {
                        Logger.Error($"PLC Read Success - device:{device.Replace("\n", "/")}, size:{size}, value:{values[0]}");
                        return res;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Exception on {System.Reflection.MethodBase.GetCurrentMethod().Name} >>> {ex.Message}\r\n{ex.StackTrace}");
                res = -1;
            }
            finally { }

            return res;
        }

        public int WriteToPLCBlock(string device, int size, ref int[] values)
        {
            int res = 0;
            try
            {
                lock (aut)
                {
                    object r = aut.WriteDeviceBlock(device, size, ref values[0]);
                    if (Convert.ToInt32(r) != 0)
                    {
                        aut.Close();

                        Thread.Sleep(500);
                        var openRs = aut.Open();
                        r = aut.WriteDeviceBlock(device, size, ref values[0]);
                        if (!r.ToString().Contains("0"))
                        {
                            Logger.Error($"{System.Reflection.MethodBase.GetCurrentMethod().Name}(): PLC Interface Failed. err code: 0x{Convert.ToString(Convert.ToInt32(r), 16)}");
                            res = -1;
                        }
                    }
                    else
                    {
                        // DoNothing();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Exception on {System.Reflection.MethodBase.GetCurrentMethod().Name} >>> {ex.Message}\r\n{ex.StackTrace}");
                res = -1;
            }
            finally { }

            return res;
        }

        public int WriteToPLCRandom(string device, int size, ref int[] values)
        {
            int res = 0;
            try
            {
                lock (aut)
                {
                    object r = aut.WriteDeviceRandom(device, size, ref values[0]);
                    if (Convert.ToInt32(r) != 0)
                    {
                        aut.Close();

                        Thread.Sleep(500);
                        var openRs = aut.Open();
                        r = aut.WriteDeviceRandom(device, size, ref values[0]);
                        if (!r.ToString().Contains("0"))
                        {
                            Logger.Error($"{System.Reflection.MethodBase.GetCurrentMethod().Name}(): PLC Interface Failed. err code: 0x{Convert.ToString(Convert.ToInt32(r), 16)}");
                            res = -1;
                        }
                    }
                    else
                    {
                        // DoNothing();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Exception on {System.Reflection.MethodBase.GetCurrentMethod().Name} >>> {ex.Message}\r\n{ex.StackTrace}");
                res = -1;
            }
            finally { }

            return res;
        }

        public int GetAPLCValue(string device, ref int readValue)
        {
            int res = 0;
            object obj = new object();

            try
            {
                lock (aut)
                {
                    for (int i = 0; i < RetryNumLimit; i++)
                    {
                        obj = aut.GetDevice(device, out readValue);
                        if (Convert.ToInt32(obj) == 0)
                        {
                            Logger.Debug($"PLC Read Success - device:{device}, value:{readValue}");
                            return res;
                        }
                        else
                        {
                            Logger.Error($"{System.Reflection.MethodBase.GetCurrentMethod().Name} >>> Retry to Read PLC Value. Addr:{device}, Melsec errcode:0x{Convert.ToString(Convert.ToInt32(obj), 16)}");
                            Thread.Sleep(300);
                        }
                    }

                    aut.Close();
                    Thread.Sleep(500);
                    var openRs = aut.Open();
                    obj = aut.GetDevice(device, out readValue);
                    //if (!obj.ToString().Contains("0"))
                    if (Convert.ToInt32(obj) != 0)
                    {
                        Logger.Error($"{System.Reflection.MethodBase.GetCurrentMethod().Name}(): PLC Interface Failed. err code: 0x{Convert.ToString(Convert.ToInt32(obj), 16)}");
                        res = -1;
                    }
                    else
                    {
                        Logger.Info($"PLC Read Success - device:{device}, value:{readValue}");
                        return res;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Exception on {System.Reflection.MethodBase.GetCurrentMethod().Name} >>> {ex.Message}\r\n{ex.StackTrace}");
                res = -1;
            }
            finally { }

            return res;
        }

        public int SetAPLCValue(string device, int value)
        {
            int res = 0;
            object obj = new object();

            try
            {
                lock (aut)
                {
                    for (int i = 0; i < RetryNumLimit; i++)
                    {
                        obj = aut.SetDevice(device, value);
                        if (Convert.ToInt32(obj) == 0)
                        {
                            Logger.Debug($"PLC Write Success - device:{device}, value:{value}");
                            return res;
                        }
                        else
                        {
                            Logger.Error($"{System.Reflection.MethodBase.GetCurrentMethod().Name} >>> Retry to Write a Value to PLC. Addr:{device}, Melsec errcode:0x{Convert.ToString(Convert.ToInt32(obj), 16)}");
                            Thread.Sleep(300);
                        }
                    }

                    aut.Close();
                    Thread.Sleep(500);
                    var openRs = aut.Open();
                    obj = aut.SetDevice(device, value);
                    if (Convert.ToInt32(obj) != 0)
                    {
                        Logger.Error($"{System.Reflection.MethodBase.GetCurrentMethod().Name}(): PLC Interface Failed. err code: 0x{Convert.ToString(Convert.ToInt32(obj), 16)}");
                        res = -1;
                    }
                    else
                    {
                        Logger.Debug($"PLC Write Success - device:{device}, value:{value}");
                        return res;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Exception on {System.Reflection.MethodBase.GetCurrentMethod().Name} >>> {ex.Message}\r\n{ex.StackTrace}");
                res = -1;
            }
            finally { }

            return res;
        }

        public override int SetAPLCValueOff(string device)
        {
            return SetAPLCValue(device, 0);
        }

        public override int SetAPLCValueOn(string device)
        {
            //LGD NG Cell test
            /*if (device == "B1F00")
            {
                SetAPLCValue("D7520", 10);
                SetAPLCValue("D7521", 11);
            }*/
            return SetAPLCValue(device, 1);
        }

        public async Task<int> AlarmBitOnAndOff(string deviceAlarm)
        {
            int returnVal = 0;
            try
            {
                returnVal = SetAPLCValueOn(deviceAlarm);
                await Task.Delay(500);
                returnVal = SetAPLCValueOff(deviceAlarm);
                Logger.Info($"Send Anomaly (On and Off) to device:{deviceAlarm}, result:{returnVal}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Exception on {System.Reflection.MethodBase.GetCurrentMethod().Name} >>> {ex.Message}\r\n{ex.StackTrace}");
            }
            return returnVal;
        }

        public async Task<int> ReleaseBitOnAndOff(string deviceRelease)
        {
            return await AlarmBitOnAndOff(deviceRelease);
        }

        public async Task<int> AlarmBitOn(string deviceAlarm)
        {
            int returnVal = SetAPLCValueOn(deviceAlarm);
            Logger.Info($"Send Anomaly to device:{deviceAlarm}, result:{returnVal}");
            await Task.Delay(10);
            return returnVal;
        }

        public async Task<int> ReleaseBitOn(string deviceRelease)
        {
            return await AlarmBitOn(deviceRelease);
        }

        public async Task<int> AlarmBitOff(string deviceAlarm)
        {
            int returnVal = SetAPLCValueOff(deviceAlarm);
            Logger.Info($"Send Release to device:{deviceAlarm}, result:{returnVal}");
            await Task.Delay(10);
            return returnVal;
        }

        public async Task<int> AlarmToggle(string device1, string device2)
        {
            int returnVal2 = SetAPLCValueOff(device2);
            int returnVal1 = SetAPLCValueOn(device1) * 10;
            Logger.Info($"Send Anomaly to device:{device1}, Release to device:{device2}, result:{returnVal1 + returnVal2}");
            await Task.Delay(10);
            return returnVal1 + returnVal2;
        }
    }
}
