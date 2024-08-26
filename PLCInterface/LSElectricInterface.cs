using System;
using System.Collections.Generic;
using System.Configuration;
using System.Text.RegularExpressions;
using VagabondK.Protocols.Channels;
using VagabondK.Protocols.LSElectric;
using VagabondK.Protocols.LSElectric.FEnet;
using System.Linq;
using Newtonsoft.Json;
using System.Threading.Tasks;
using System.Threading;

namespace PLCInterface
{
    class LSElectricInterface : CommonInterface
    {
        public FEnetClient client;
        private static int RetryNumLimit { get; set; } = 3;
        //public string DeviceRandomToRead { get; set; } = string.Empty;
        //public string DeviceRandomToWrite { get; set; } = string.Empty;
        public List<DeviceVariable> DeviceRandomToRead = new List<DeviceVariable>();
        public List<DeviceVariable> DeviceRandomToWrite = new List<DeviceVariable>();
        int[] readValues;
        List<PlcVariable> ReadDevices;
        List<PlcVariable> WriteDevices;
        //private Task plcInterfaceTask;
        private string PreviousRead { get; set; } = string.Empty;

        public LSElectricInterface()
        {
            try
            {
                #region Read Configuration

                int interfaceIndex = 0;
                ReadDevices = new List<PlcVariable>();

                int j;
                for (j = 0; j < GlobalInfo.NumChannel; j++)
                {
                    int modelQty = Convert.ToInt32(ConfigurationManager.AppSettings["ModelQty"] ?? "1");
                    string deviceModel = ConfigurationManager.AppSettings["ModelStartAddress"] ?? string.Empty;
                    if (j > 0)
                    {
                        modelQty = Convert.ToInt32(ConfigurationManager.AppSettings[$"ModelQty{j + 1}"] ?? "1");
                        deviceModel = ConfigurationManager.AppSettings[$"ModelStartAddress{j + 1}"] ?? string.Empty;
                    }
                    if (modelQty > 1 && deviceModel.Length > 1)
                    {
                        string prefix = Regex.Replace(deviceModel, @"[^a-zA-Z]", "");
                        int number = Convert.ToInt32(Regex.Replace(deviceModel, @"[^0-9]", ""));

                        for (int i = 0; i < modelQty; i++)
                        {
                            var plcVar = new PlcVariable(interfaceIndex++, $"Model{i + 1}", $"{prefix}{number++}", j + 1);
                            ReadDevices.Add(plcVar);
                        }
                    }
                }

                for (j = 0; j < GlobalInfo.NumChannel; j++)
                {
                    CheckPlcAddress(interfaceIndex++, "TriggerAddress", true, j + 1);
                    CheckPlcAddress(interfaceIndex++, "MbbTriggerAddress", true, j + 1);
                    CheckPlcAddress(interfaceIndex++, "ReadyAddress", true, j + 1);
                    CheckPlcAddress(interfaceIndex++, "Word1ModelAddress", true, j + 1);
                    CheckPlcAddress(interfaceIndex++, "Word2ModelAddress", true, j + 1);
                    CheckPlcAddress(interfaceIndex++, "ESMIModelChangeRequestAddress", true, j + 1);
                    CheckPlcAddress(interfaceIndex++, "ModeSelectAddress", true, j + 1);
                    CheckPlcAddress(interfaceIndex++, "CommErrorAddress", true, j + 1);
                    CheckPlcAddress(interfaceIndex++, "ESMIModelNumberAddress", true, j + 1);
                    CheckPlcAddress(interfaceIndex++, "NestNumberAddress", true, j + 1);
                }
                #endregion Read Configuration

                #region Write Configuration

                interfaceIndex = 0;
                WriteDevices = new List<PlcVariable>();

                for (j = 0; j < GlobalInfo.NumChannel; j++)
                {
                    CheckPlcAddress(interfaceIndex++, "AnomalyOnAddress", false, j+1);
                    CheckPlcAddress(interfaceIndex++, "AnomalyOffAddress", false, j+1);
                }
                CheckPlcAddress(interfaceIndex++, "AliveAddress", false);
                CheckPlcAddress(interfaceIndex++, "CaptureCompleteAddress", false);
                CheckPlcAddress(interfaceIndex++, "BusyAddress", false);
                CheckPlcAddress(interfaceIndex++, "ESMIModelChangeCompleteAddress", false);
                CheckPlcAddress(interfaceIndex++, "ErrorAddress", false);
                CheckPlcAddress(interfaceIndex++, "DetectReadyAddress", false);
                CheckPlcAddress(interfaceIndex++, "AliveAddress", false);
                CheckPlcAddress(interfaceIndex++, "LearnModeAddress", false);

                #endregion Write Configuration

                GetDevicesAddressRandom();

                readValues = new int[ReadDevices.Count];

                IsConfigurationSuccess = true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Exception on {System.Reflection.MethodBase.GetCurrentMethod().Name} >>> {ex.Message}\r\n{ex.StackTrace}");
                IsConfigurationSuccess = false;
            }
            finally { }
        }

        private void GetDevicesAddressRandom()
        {
            DeviceRandomToRead.Clear();
            DeviceRandomToWrite.Clear();

            foreach (PlcVariable item in ReadDevices)
            {
                if (item.DeviceAddress[0] == '%')
                {
                    DeviceVariable deviceToRead = DeviceVariable.Parse(item.DeviceAddress);
                    DeviceRandomToRead.Add(deviceToRead);
                }
            }

            foreach (PlcVariable item in WriteDevices)
            {
                if (item.DeviceAddress[0] == '%')
                {
                    DeviceVariable deviceToWrite = DeviceVariable.Parse(item.DeviceAddress);
                    DeviceRandomToWrite.Add(deviceToWrite);
                }
            }
        }

        public override string StartInterface()
        {
            string returnMessage = string.Empty;
            try
            {
                client = new FEnetClient(new TcpChannel(GlobalInfo.LSElectricIP, GlobalInfo.LSElectricPort));

                if (client != null)
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
            catch (Exception ex)
            {
                Logger.Error($"Exception on {System.Reflection.MethodBase.GetCurrentMethod().Name} >>> {ex.Message}\r\n{ex.StackTrace}");
                returnMessage = ex.Message;
            }
            finally { }

            return returnMessage;
        }

        public override int OpenConnection()
        {
            return 0;
        }
        
        public override int CloseConnection()
        {
            return 0;
        }

        public override string ReadPlcValues()
        {
            try
            {
                if (ReadFromPLCRandom(DeviceRandomToRead, ReadDevices.Count, ref readValues) != 0)
                {
                    //Logger.Error($"PLC read error on {System.Reflection.MethodBase.GetCurrentMethod().Name}. device:[{DeviceRandomToRead.Replace("\n", "/")}]");
                    //return $"PLC Read Error: [{DeviceRandomToRead.Replace("\n", "/")}]";
                    return $"PLC Read Error";
                }
                else
                {
                    int i = 0;
                    foreach (PlcVariable item in ReadDevices)
                    {
                        item.ReadValue = readValues[i++];
                    }
                    string json = JsonConvert.SerializeObject(ReadDevices);
                    //Task.Run(() => HttpMessage.SendHttpMessage(json));
                    var messageTasks = new List<Task>();
                    for (int j = 0; j < GlobalInfo.NumChannel; j++)
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
                    if (itemReady != null && itemReady.ReadValue == 0)
                    {
                        SetAPLCValueOn(itemReady.DeviceAddress);
                    }

                    if (PreviousRead != string.Join(" ", readValues))
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

        public int ReadFromPLCRandom(List<DeviceVariable> devices, int size, ref int[] values)
        {
            int res = 0;

            try
            {
                lock (client)
                {
                    int i = 0;
                    foreach (var item in client.Read(devices))
                    {
                        values[i++] = item.Value.WordValue;
                    }
                    //Logger.Error($"PLC Read Success - device:{devices.Replace("\n", "/")}, size:{i}, value:{values[0]}");
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

            try
            {
                lock (client)
                {
                    var item = client.Read(device).ToArray();
                    readValue = item[0].Value.WordValue;
                    Logger.Debug($"PLC Read Success - device:{device}, value:{readValue}");
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
            if (device == string.Empty)
            {
                //Logger.Error("Write Device is empty");
                return -1;
            }

            int res = 0;

            try
            {
                lock (client)
                {
                    client.Write(device, value);
                    Logger.Debug($"PLC Write Success - device:{device}, value:{value}");
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
            return SetAPLCValue(device, 1);
        }

        public override int GetReadDevicesCount()
        {
            return ReadDevices.Count;
        }

        public override int WriteToPLCRandomString(string device, string text)
        {
            int dummy = 0;
            return dummy;
        }
        public override string ReadFromPLCRandomString(string device, int size)
        {
            string dummy = "";
            return dummy;
        }
    }
}