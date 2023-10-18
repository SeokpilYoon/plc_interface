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
using System.Runtime.InteropServices;

namespace PLCInterface
{
    class ADLinkDIOInterface : CommonInterface
    {
        [DllImport("C:\\ADLINK\\PCIS-DASK\\Lib\\PCI-Dask.dll")] public static extern short Register_Card(ushort CardType, ushort card_num);
        [DllImport("C:\\ADLINK\\PCIS-DASK\\Lib\\PCI-Dask.dll")] public static extern short Release_Card(ushort CardNumber);
        [DllImport("C:\\ADLINK\\PCIS-DASK\\Lib\\PCI-Dask.dll")] public static extern short DI_ReadPort(ushort CardNumber, ushort Port, out uint Value);
        [DllImport("C:\\ADLINK\\PCIS-DASK\\Lib\\PCI-Dask.dll")] public static extern short DO_WritePort(ushort CardNumber, ushort Port, uint Value);
        public const ushort PCI_7230 = 6;

        short card = 0;
        int DIO_TriggerPort = 0;
        List<PlcVariable> ReadDevices;
        List<PlcVariable> WriteDevices;
        int[] readValues;
        private string PreviousRead { get; set; } = string.Empty;

        public ADLinkDIOInterface()
        {
            try
            {
                IsConfigurationSuccess = true;

                DIO_TriggerPort = Convert.ToInt32(ConfigurationManager.AppSettings["TriggerAddress"] ?? "0");

                #region Read Configuration

                int interfaceIndex = 0;
                ReadDevices = new List<PlcVariable>();

                /*int j;
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
                }*/

                //for (j = 0; j < GlobalInfo.NumChannel; j++)
                {
                    CheckPlcAddress(interfaceIndex++, "TriggerAddress", true);//, j + 1);
                    //CheckPlcAddress(interfaceIndex++, "MbbTriggerAddress", true, j + 1);
                    //CheckPlcAddress(interfaceIndex++, "ReadyAddress", true, j + 1);
                }

                #endregion Read Configuration

                #region Write Configuration

                interfaceIndex = 0;
                WriteDevices = new List<PlcVariable>();

                CheckPlcAddress(interfaceIndex++, "AnomalyOnAddress", false);
                CheckPlcAddress(interfaceIndex++, "AnomalyOffAddress", false);

                #endregion Write Configuration

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

        public override string StartInterface()
        {
            string returnMessage = string.Empty;
            try
            {
                card = Register_Card(PCI_7230, 0/*card_number*/);
                if (card < 0)
                {
                    returnMessage = $"ADLink DIO connection is Failed, Register_Card Error = {card}";
                    Logger.Error(returnMessage);
                }
                else
                {
                    IsAlive = true;
                    returnMessage = $"ADLink DIO connection is success";
                    Logger.Info(returnMessage);
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
            //Logger.Info($"{System.Reflection.MethodBase.GetCurrentMethod().Name}(): AdvantechDAQ Connection is Success");
            return 0;
        }

        public override int CloseConnection()
        {
            if (card >= 0)
                Release_Card((ushort)card);

            IsAlive = false;

            Logger.Info($"{System.Reflection.MethodBase.GetCurrentMethod().Name}(): ADLink DIO Close Connection is Success");

            return 0;
        }

        public override string ReadPlcValues()
        {
            try
            {
                uint portData = 0;
                short err = DI_ReadPort((ushort)card, 0, out portData);
                /*if (err != ErrorCode.Success)
                {
                    Logger.Error($"PLC read error on {System.Reflection.MethodBase.GetCurrentMethod().Name}. port:[{GlobalInfo.DIO_TriggerPort}], Error : {err}");
                    return $"PLC Read Error";
                }*/

                Logger.Debug($"PLC read Success [{portData}]");

                uint triggerData = (uint)((portData >> DIO_TriggerPort) & 0x01);

                Logger.Debug($"triggerData [{triggerData}], DIO_TriggerPort [{DIO_TriggerPort}]");

                foreach (PlcVariable item in ReadDevices)
                {
                    if (item.DeviceAddress == "TriggerAddress")
                    {
                        item.ReadValue = (int)triggerData;
                    }
                }

                // 비동기로 보낸다
                //string json = JsonConvert.SerializeObject(ReadDevices);
                //Task.Run(() => HttpMessage.SendHttpMessage(json));
                var messageTasks = new List<Task>();
                ReadDevices[0].ReadValue = (int)triggerData;
                for (int j = 0; j < GlobalInfo.NumChannel; j++)
                {
                    ReadDevices[0].ChannelNo = j + 1;
                    string json = JsonConvert.SerializeObject(ReadDevices);
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

                if (PreviousRead != string.Join(" ", readValues))
                {
                    PreviousRead = string.Join(" ", readValues);
                    Logger.Trace(PreviousRead);
                }
                return $"ADLink DIO Read Success [{string.Join(" ", triggerData)}]";
            }
            catch (Exception ex)
            {
                Logger.Error($"Exception on {System.Reflection.MethodBase.GetCurrentMethod().Name} >>> {ex.Message}\r\n{ex.StackTrace}");
                return ex.Message;
            }
            finally { }
        }

        // OK/NG는 2초 뒤에 지워야함 (코드 추가 필요)
        public override int SetAPLCValueOff(string device)
        {
            if (device == string.Empty)
            {
                Logger.Error($"PLC write Addr is empty");
                return 0;
            }

            int deviceNum = Convert.ToInt16(device);

            short err = DO_WritePort((ushort)card, 0, (uint)0);   // 0 쓰는거 요거 생각 좀 더 해봐야함, 다른 포트도 지워짐
            /*if (err != ErrorCode.Success)
            {
                Logger.Error($"PLC write error on {System.Reflection.MethodBase.GetCurrentMethod().Name}. port:[{port}], Error : {err}");
            }*/

            Logger.Debug($"PLC write Success {System.Reflection.MethodBase.GetCurrentMethod().Name}. port:[{device}], Value : 0");

            return (int) err;
        }

        public override int SetAPLCValueOn(string device)
        {
            if(device == string.Empty)
            {
                Logger.Error($"PLC write Addr is empty");
                return 0;
            }

            int deviceNum = Convert.ToInt16(device);

            int output = ((ushort)1L) << deviceNum;
            output ^= ((ushort)1L) << 2;
            short err = DO_WritePort((ushort)card, 0, (uint)output);
            /*if (err != ErrorCode.Success)
            {
                Logger.Error($"PLC write error on {System.Reflection.MethodBase.GetCurrentMethod().Name}. port:[{port}], Error : {err}");
            }*/

            Logger.Debug($"PLC write Success {System.Reflection.MethodBase.GetCurrentMethod().Name}. port:[{device}], Value : 1");

            return (int) err;
        }

        // SPY TEST
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

        public override int GetReadDevicesCount()
        {
            return 1;
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
    }
}