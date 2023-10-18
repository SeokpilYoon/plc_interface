using System;
using System.Collections.Generic;
using System.Configuration;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Automation.BDaq;
using Newtonsoft.Json;

namespace PLCInterface
{
    class AdvantechDAQInterface : CommonInterface
    {
        private Automation.BDaq.InstantDiCtrl instantDiCtrl1;
        private Automation.BDaq.InstantDoCtrl instantDoCtrl1;
        bool UseDongaScenario = false;
        int DIO_DeviceNumber = 0;
        int DIO_TriggerPort = 0;
        int DIO_TriggerOnPort = 0;
        int DIO_TriggerOffPort = 0;
        bool TriggerStatus = false;
        bool TriggerOffData = false;

        List<PlcVariable> ReadDevices;
        List<PlcVariable> WriteDevices;

        private System.Timers.Timer ReportTimer = null;
        AuroraUSBTowerLampInterface auroraUSBTowerLampInterface;

        public AdvantechDAQInterface()
        {
            try
            {
                instantDiCtrl1 = new Automation.BDaq.InstantDiCtrl();
                instantDoCtrl1 = new Automation.BDaq.InstantDoCtrl();                
                //instantDoCtrl1._StateStream = ((Automation.BDaq.DeviceStateStreamer)(resources.GetObject("instantDoCtrl1._StateStream")));

                UseDongaScenario = (ConfigurationManager.AppSettings["UseDongaScenario"] ?? string.Empty).ToUpper().Equals("TRUE");
                DIO_DeviceNumber = Convert.ToInt32(ConfigurationManager.AppSettings["DIO_DeviceNumber"] ?? "0");
                DIO_TriggerPort = Convert.ToInt32(ConfigurationManager.AppSettings["DIO_TriggerPort"] ?? "0");
                DIO_TriggerOnPort = Convert.ToInt32(ConfigurationManager.AppSettings["DIO_TriggerOnPort"] ?? "0");
                DIO_TriggerOffPort = Convert.ToInt32(ConfigurationManager.AppSettings["DIO_TriggerOffPort"] ?? "0");

                #region Read Configuration
                int interfaceIndex = 0;
                ReadDevices = new List<PlcVariable>();
                CheckPlcAddress(interfaceIndex++, "TriggerAddress", true);
                #endregion

                ReportTimer = new System.Timers.Timer
                {
                    Interval = 100,
                    AutoReset = true,
                    Enabled = false
                };
                ReportTimer.Elapsed += new ElapsedEventHandler(ReportTimerHandler);

                if (GlobalInfo.UseAuroraTowerLamp == true)
                {
                    auroraUSBTowerLampInterface = new AuroraUSBTowerLampInterface();
                    auroraUSBTowerLampInterface.StartInterface();
                }

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
                instantDiCtrl1.SelectedDevice = new DeviceInformation(DIO_DeviceNumber);
                instantDoCtrl1.SelectedDevice = new DeviceInformation(DIO_DeviceNumber);

                IsAlive = true;

                Logger.Debug($"Advantech DAQ started");
            }
            catch (Exception ex)
            {
                IsAlive = false;
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
            Logger.Info($"{System.Reflection.MethodBase.GetCurrentMethod().Name}(): AdvantechDAQ Close Connection is Success");
            IsConfigurationSuccess = false;
            IsAlive = false;

            return 0;
        }

        public override string ReadPlcValues()
        {
            try
            {
                // read Di port state
                byte port0Data = 0;
                byte port1Data = 0;
                ErrorCode err = ErrorCode.Success;

                // bit 고려하지 않아 임시로 trigger 주로 0으로 변경함 (port0 읽기 위해)
                // 원래는 1임

                err = instantDiCtrl1.Read(0, out port0Data);
                if (err != ErrorCode.Success)
                {
                    Logger.Error($"PLC read error on {System.Reflection.MethodBase.GetCurrentMethod().Name}. port 0, Error : {err}");
                    return $"PLC Read Error";
                }
                err = instantDiCtrl1.Read(1, out port1Data);
                if (err != ErrorCode.Success)
                {
                    Logger.Error($"PLC read error on {System.Reflection.MethodBase.GetCurrentMethod().Name}. port 1, Error : {err}");
                    return $"PLC Read Error";
                }
                ushort inputData = (ushort)(port0Data + ((ushort)port1Data << 8));

                if (UseDongaScenario == true)
                {
                    int triggerOnData = (inputData >> DIO_TriggerOnPort) & 0x1;
                    int triggerOffData = (inputData >> DIO_TriggerOffPort) & 0x1;
                    if (triggerOnData == 1)
                    {
                        if (TriggerStatus == false)
                        {
                            TriggerStatus = true;
                            ReportTimer.Enabled = false;
                            GlobalInfo.DongaResults.Clear();

                            // Send Trigger On
                            foreach (PlcVariable item in ReadDevices)
                            {
                                if (item.DeviceAddress == "TriggerAddress")
                                {
                                    item.ReadValue = 1;
                                }
                            }

                            // 비동기로 보낸다
                            var messageTasks = new List<Task>();
                            ReadDevices[0].ReadValue = 1;
                            for (int j = 0; j < GlobalInfo.NumChannel; j++)
                            {
                                if (GlobalInfo.EnableChannels[j] == false)
                                    continue;

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
                            Logger.Debug("Pooling 'TRIGGER_On' message from DIO");
                        }
                    }
                    if (triggerOffData == 1)
                    {
                        TriggerOffData = true;
                        Logger.Debug("Pooling 'TRIGGER_Off_SensorOn' message from DIO");
                    }
                    if (triggerOffData == 0)
                    {
                        if (TriggerStatus == true && TriggerOffData == true)
                        {
                            TriggerOffData = false;
                            TriggerStatus = false;

                            // Send Trigger Off
                            foreach (PlcVariable item in ReadDevices)
                            {
                                if (item.DeviceAddress == "TriggerAddress")
                                {
                                    item.ReadValue = 0;
                                }
                            }

                            // 비동기로 보낸다
                            var messageTasks = new List<Task>();
                            ReadDevices[0].ReadValue = 0;
                            for (int j = 0; j < GlobalInfo.NumChannel; j++)
                            {
                                if (GlobalInfo.EnableChannels[j] == false)
                                    continue;

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
                            Logger.Debug("Pooling 'TRIGGER_Off' message from DIO");

                            ReportTimer.Enabled = true;
                        }
                    }
                }
                else
                {
                    int triggerData = (inputData >> DIO_TriggerPort) & 0x1;

                    foreach (PlcVariable item in ReadDevices)
                    {
                        if (item.DeviceAddress == "TriggerAddress")
                        {
                            item.ReadValue = triggerData;
                        }
                    }

                    // 비동기로 보낸다
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
                }

                return $"PLC Read Success [0x{inputData.ToString("X4")}]";
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
            try
            {
                if (UseDongaScenario == true)
                    return (int)ErrorCode.Success;

                if (device == string.Empty)
                {
                    Logger.Error($"PLC write Addr is empty");
                    return 0;
                }

                int deviceNum = Convert.ToInt16(device);
                int port = deviceNum / 8;

                ErrorCode err = instantDoCtrl1.Write(port, (byte)0);
                if (err != ErrorCode.Success)
                {
                    Logger.Error($"PLC write error on {System.Reflection.MethodBase.GetCurrentMethod().Name}. port:[{port}], Error : {err}");
                }

                Logger.Error($"PLC write Success {System.Reflection.MethodBase.GetCurrentMethod().Name}. port:[{port}], Value : 0");

                return (int)err;
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
                if (UseDongaScenario == true)
                    return (int)ErrorCode.Success;

                if (device == string.Empty)
                {
                    Logger.Error($"PLC write Addr is empty");
                    return 0;
                }

                int deviceNum = Convert.ToInt16(device);
                int port = deviceNum / 8;
                int num = deviceNum % 8;
                int value = 1 << num;
                // Write 는 8bit씩 됨, 단자 0~7 --> port0, 단자 8~15 --> port1 이런식임
                // 따라서 내가 쓰려는것 외에 다른 단자가 지워지지 않는것도 추후 고려해야함
                // (일단은 off를 안하게 하고, Ready와 OK/NG를 다른 port로 설정함)
                ErrorCode err = instantDoCtrl1.Write(port, (byte)value);
                if (err != ErrorCode.Success)
                {
                    Logger.Error($"PLC write error on {System.Reflection.MethodBase.GetCurrentMethod().Name}. port:[{port}], Error : {err}");
                }

                Logger.Error($"PLC write Success {System.Reflection.MethodBase.GetCurrentMethod().Name}. port:[{port}], Value : {value}");

                return (int)err;
            }
            catch (Exception ex)
            {
                Logger.Error($"Exception on {System.Reflection.MethodBase.GetCurrentMethod().Name} >>> {ex.Message}\r\n{ex.StackTrace}");
                return 0;
            }
            finally { }
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

        private void ReportTimerHandler(object source, ElapsedEventArgs e)
        {
            if (GlobalInfo.DongaResults.Count == GlobalInfo.NumEnabledChannel)
            {
                ReportTimer.Enabled = false;
                int reportChannel = 0;

                for (int i = 0; i < GlobalInfo.DongaResults.Count; i++)
                {
                    if (GlobalInfo.DongaResults[i].OK_YN == "N")
                    {
                        reportChannel = i;
                        break;
                    }
                }

                Utility.SendOracleDB(GlobalInfo.DongaResults[reportChannel].PART_NO, GlobalInfo.DongaResults[reportChannel].LINE_CD, GlobalInfo.DongaResults[reportChannel].TEST_DT, GlobalInfo.DongaResults[reportChannel].OK_YN, GlobalInfo.DongaResults[reportChannel].IMAGEPATH, GlobalInfo.DongaResults[reportChannel].CAMERA_NO);
                if (GlobalInfo.UseAuroraTowerLamp == true)
                {
                    if (GlobalInfo.DongaResults[reportChannel].OK_YN == "Y")
                        auroraUSBTowerLampInterface.SetLed(false, false, true, false, false);
                    else
                        auroraUSBTowerLampInterface.SetLed(GlobalInfo.USBLamp_AlarmRed, GlobalInfo.USBLamp_AlarmYellow, false, GlobalInfo.USBLamp_AlarmBuzzer, false);
                }
                GlobalInfo.DongaResults.Clear();
            }
        }
    }
}