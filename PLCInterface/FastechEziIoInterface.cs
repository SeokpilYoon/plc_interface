using System;
using System.Collections.Generic;
using System.Configuration;
using Newtonsoft.Json;
using System.Runtime.InteropServices;   //import하지 않으면 DLLImport 사용 불가!
using FASTECH;
using System.Net;
using System.Threading.Tasks;
using System.Threading;
using VagabondK.Protocols.LSElectric;
using VagabondK.Protocols;

namespace PLCInterface
{
    class FastechEziIoInterface : CommonInterface
    {
        /* 
         * - c# 예제 파일은 S/W 설치 후 C:\Program Files (x86)\FASTECH\Ezi-MOTION Plus-E V6\Example\C# 경로에 있습니다.
         * - Ezi-IO-I8O8 제품(Input 8점/ Output 8점 혼합 제품)만 적용 가능하도록 코드 반영되어 있습니다. (V.1.6.25)
         * - Input+Output 혼합 제품 (Ezi-IO-I8O8, Ezi-IO-I16O16 등)은 Input의 BitMask를 전부 할당한 뒤, Output의 BitMask가 이어서 할당되어있습니다.
         *   예를들어 Ezi-IO-I8O8의 Input 0번의 BitMask는 0x0001, Output 0번의 BitMask는 0x0100입니다.         
         *   Output 0번 : 0x0100  / 1번 : 0x0200 / 2번 : 0x0400 / 3번 : 0x0800 / 4번 : 0x1000 ~ 7번 : 0x8000
         * - uSetMask는 Set할 Bit Array, uClrMask는 Clear할 Bit Array입니다.
         */

        int[] readValues;
        List<PlcVariable> ReadDevices;
        List<PlcVariable> WriteDevices;
        private string PreviousRead { get; set; } = string.Empty;

        public FastechEziIoInterface()
        {
            try
            {
                #region Read Configuration

                int interfaceIndex = 0;
                ReadDevices = new List<PlcVariable>();

                int j;
                for (j = 0; j < GlobalInfo.NumChannel; j++)
                {
                    CheckPlcAddress(interfaceIndex++, "TriggerAddress", true, j + 1);
                }
                #endregion Read Configuration

                #region Write Configuration

                interfaceIndex = 0;
                WriteDevices = new List<PlcVariable>();

                for (j = 0; j < GlobalInfo.NumChannel; j++)
                {
                    CheckPlcAddress(interfaceIndex++, "AnomalyOnAddress", false, j + 1);
                    CheckPlcAddress(interfaceIndex++, "AnomalyOffAddress", false, j + 1);
                }
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

        public override string StartInterface()
        {
            string returnMessage = string.Empty;
            try
            {
                if (OpenConnection() == 0)
                {
                    IsAlive = true;
                    returnMessage = $"DIO connection is success";
                    Logger.Info(returnMessage);
                }
                else
                {
                    returnMessage = "DIO connection is Failed";
                    Logger.Error(returnMessage);
                }

                Logger.Debug($"Fastech Ezi-IO started");
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
            if (Connect(TCP, nBdID) == false)
            {
                return 1;
            }
            return 0;
        }

        public const int TCP = 0;
        public const int UDP = 1;
        int nBdID = 0;

        static bool Connect(int nCommType, int nBdID)
        {
            bool bSuccess = true;

            try
            {
                string ipstring = ConfigurationManager.AppSettings["EziIO_IP"] ?? "192.168.0.2";
                string[] address = ipstring.Split('.'); // ?modeltype=x
                byte[] num = new byte[4];

                for (int i = 0; i < address.Length; i++)
                {
                    num[i] = (byte)System.Convert.ToInt16(address[i]);
                }

                IPAddress ip = new IPAddress(num);

                // Connection
                switch (nCommType)
                {
                    case TCP:
                        // TCP Connection
                        if (EziMOTIONPlusELib.FAS_ConnectTCP(ip, nBdID) == false)
                        {
                            Logger.Info($"[FastechEziIO] TCP Connection Fail!");
                            bSuccess = false;
                        }
                        break;

                    case UDP:
                        // UDP Connection
                        if (EziMOTIONPlusELib.FAS_Connect(ip, nBdID) == false)
                        {
                            Logger.Info($"[FastechEziIO] UDP Connection Fail!");
                            bSuccess = false;
                        }
                        break;

                    default:
                        //Console.WriteLine("[FastechEziIO] Wrong communication type.");
                        bSuccess = false;

                        break;
                }

                if (bSuccess)
                    Logger.Info($"[FastechEziIO] Connected successfully.");
            }
            catch (Exception ex)
            {
                Logger.Error($"Exception on {System.Reflection.MethodBase.GetCurrentMethod().Name} >>> {ex.Message}\r\n{ex.StackTrace}");
                bSuccess = false;
                return bSuccess;
            }
            finally { }
            return bSuccess;
        }

        static bool CheckDriveInfo(int nBdID)
        {
            byte byType = 0;
            string version = "";
            int nRtn;

            // Read Drive's information
            nRtn = EziMOTIONPlusELib.FAS_GetSlaveInfo(nBdID, ref byType, ref version);
            if (nRtn != EziMOTIONPlusELib.FMM_OK)
            {
                Logger.Info($"[FastechEziIO] Can't read DIO board status.");
                return false;
            }
            Logger.Info($"[FastechEziIO] Board ID {nBdID} : TYPE= {byType}, Version= {version}");

            return true;
        }

        public override int CloseConnection()
        {
            try
            {
                // Connection Close
                EziMOTIONPlusELib.FAS_Close(nBdID);
                IsAlive = false;

                Logger.Info($"{System.Reflection.MethodBase.GetCurrentMethod().Name}(): AdvantechDAQ Close Connection is Success");
            }
            catch (Exception ex)
            {
                Logger.Error($"Exception on {System.Reflection.MethodBase.GetCurrentMethod().Name} >>> {ex.Message}\r\n{ex.StackTrace}");
            }
            finally { }
            return 0;
        }

        public override string ReadPlcValues()
        {
            try
            {
                bool bCloseSuccess = true;
                // 0.8.2 : DIO 보드 전원 살아있는지 확인 후 재연결                
                if (CheckDriveInfo(nBdID) == false) // Check Drive information
                {
                    try
                    {
                        EziMOTIONPlusELib.FAS_Close(nBdID); // Connection Close
                    }
                    catch
                    {
                        Logger.Info($"[FastechEziIO] Connection Close 실패");
                        bCloseSuccess = false;
                    }

                    // PLC 재연결
                    if (Connect(TCP, nBdID) && bCloseSuccess)
                    {
                        Logger.Info($"[FastechEziIO] Reconnect 성공");
                    }
                    else
                    {
                        Logger.Info($"[FastechEziIO] Reconnect 실패");
                    }
                }

                if (ReadFromPLCRandom(ref readValues) != 0)
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

        public int ReadFromPLCRandom(ref int[] values)
        {
            int res = 0;

            try
            {
                uint uInput = 0;
                uint uLatch = 0;

                if (EziMOTIONPlusELib.FAS_GetInput(nBdID, ref uInput, ref uLatch) != EziMOTIONPlusELib.FMM_OK)
                {
                    Logger.Error($"IO signal read failed.");
                    return -1;
                }

                int i = 0;
                foreach (var item in ReadDevices)
                {
                    int triggerData = ((uInput & (0x01 << Convert.ToInt32(item.DeviceAddress))) != 0) ? 1 : 0;
                    values[i++] = triggerData;
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
            Logger.Error($"Not supported");
            return 0;
        }

        public override int SetAPLCValueOff(string device)
        {
            try
            {
                /* 
                 * - c# 예제 파일은 S/W 설치 후 C:\Program Files (x86)\FASTECH\Ezi-MOTION Plus-E V6\Example\C# 경로에 있습니다.
                 * - Ezi-IO-I8O8 제품(Input 8점/ Output 8점 혼합 제품)만 적용 가능하도록 코드 반영되어 있습니다. (V.1.6.25)
                 * - Input+Output 혼합 제품 (Ezi-IO-I8O8, Ezi-IO-I16O16 등)은 Input의 BitMask를 전부 할당한 뒤, Output의 BitMask가 이어서 할당되어있습니다.
                 *   예를들어 Ezi-IO-I8O8의 Input 0번의 BitMask는 0x0001, Output 0번의 BitMask는 0x0100입니다.         
                 *   Output 0번 : 0x0100  / 1번 : 0x0200 / 2번 : 0x0400 / 3번 : 0x0800 / 4번 : 0x1000 ~ 7번 : 0x8000
                 * - uSetMask는 Set할 Bit Array, uClrMask는 Clear할 Bit Array입니다.
                 */

                int deviceNum;
                uint uSetMask = 0x0000;
                uint uClrMask;

                // check device
                if (device == string.Empty)
                {
                    Logger.Error($"PLC write Addr is empty");
                    return 0;
                }
                if (!int.TryParse(device, out deviceNum))
                {
                    Logger.Error($"PLC write Addr is not number : {device}");
                    return 0;
                }
                if (deviceNum < 0 || deviceNum > 7)
                {
                    Logger.Error($"PLC write Addr is not in between 0~7 : {deviceNum}");
                    return 0;
                }

                // Write to DIO board
                uClrMask = (uint)(0x0100 << deviceNum);
                if (EziMOTIONPlusELib.FAS_SetOutput(nBdID, uSetMask, uClrMask) != EziMOTIONPlusELib.FMM_OK)
                {
                    Logger.Error($"PLC write error on Function(FAS_SetOutput) - device:{deviceNum}");
                    return 0;
                }

                Logger.Debug($"PLC Write (DIO) Success - device:{deviceNum}, value:{0}");
                return (int)1;
            }

            catch (Exception ex)
            {
                Logger.Error($"Exception on {System.Reflection.MethodBase.GetCurrentMethod().Name} >>> {ex.Message}\r\n{ex.StackTrace}");
                return 0;
            }
            finally { }
        }

        public override int SetAPLCValueOn(string device)
        {
            try
            {
                /* 
                 * - c# 예제 파일은 S/W 설치 후 C:\Program Files (x86)\FASTECH\Ezi-MOTION Plus-E V6\Example\C# 경로에 있습니다.
                 * - Ezi-IO-I8O8 제품(Input 8점/ Output 8점 혼합 제품)만 적용 가능하도록 코드 반영되어 있습니다. (V.1.6.25)
                 * - Input+Output 혼합 제품 (Ezi-IO-I8O8, Ezi-IO-I16O16 등)은 Input의 BitMask를 전부 할당한 뒤, Output의 BitMask가 이어서 할당되어있습니다.
                 *   예를들어 Ezi-IO-I8O8의 Input 0번의 BitMask는 0x0001, Output 0번의 BitMask는 0x0100입니다.         
                 *   Output 0번 : 0x0100  / 1번 : 0x0200 / 2번 : 0x0400 / 3번 : 0x0800 / 4번 : 0x1000 ~ 7번 : 0x8000
                 * - uSetMask는 Set할 Bit Array, uClrMask는 Clear할 Bit Array입니다.
                 */

                int deviceNum;
                uint uSetMask;
                uint uClrMask = 0x0000;

                // check device
                if (device == string.Empty)
                {
                    Logger.Error($"PLC write Addr is empty");
                    return 0;
                }
                if (!int.TryParse(device, out deviceNum))
                {
                    Logger.Error($"PLC write Addr is not number : {device}");
                    return 0;
                }
                if (deviceNum < 0 || deviceNum > 7)
                {
                    Logger.Error($"PLC write Addr is not in between 0~7 : {deviceNum}");
                    return 0;
                }

                // Write to DIO board
                uSetMask = (uint)(0x0100 << deviceNum);
                if (EziMOTIONPlusELib.FAS_SetOutput(nBdID, uSetMask, uClrMask) != EziMOTIONPlusELib.FMM_OK)
                {
                    Logger.Error($"PLC write error on Function(FAS_SetOutput) - device:{deviceNum}");
                    return 0;
                }

                Logger.Debug($"PLC Write (DIO) Success - device:{deviceNum}, value:{1}");
                return (int)1;
            }
            catch (Exception ex)
            {
                Logger.Error($"Exception on {System.Reflection.MethodBase.GetCurrentMethod().Name} >>> {ex.Message}\r\n{ex.StackTrace}");
                return 0;
            }
            finally { }
        }

        public override int GetReadDevicesCount()
        {
            return 1;
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
