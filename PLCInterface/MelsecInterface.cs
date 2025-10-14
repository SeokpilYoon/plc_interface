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
using System.Data;
using static System.Net.Mime.MediaTypeNames;
using System.Diagnostics;
using System.Reflection;
using System.Xml.Linq;
using System.Runtime.InteropServices;

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

        private static bool ModelChanging { get; set; } = false;

        private static string prevBarcodeString = string.Empty;
        private bool[] triggerDetectedPerChannel;

        private List<PlcVariable> BarcodeDataDevices = new List<PlcVariable>();
        private bool hasBarcodeProcessed = false;

        public MelsecInterface()
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
                            var plcVar = new PlcVariable(interfaceIndex++, $"Model{i + 1}", $"{prefix}{number++}", j+1);
                            ReadDevices.Add(plcVar);
                        }
                    }
                }

                for (j = 0; j < GlobalInfo.NumChannel; j++)
                {
                    CheckPlcAddress(interfaceIndex++, "TriggerAddress", true, j+1);
                    CheckPlcAddress(interfaceIndex++, "AdditionalTriggerAddress", true, j + 1);
                    CheckPlcAddress(interfaceIndex++, "MbbTriggerAddress", true, j+1);
                    CheckPlcAddress(interfaceIndex++, "ReadyAddress", true, j+1);
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
                    CheckPlcAddress(interfaceIndex++, "AnomalyOnAddress", false, j + 1);
                    CheckPlcAddress(interfaceIndex++, "AnomalyOffAddress", false, j + 1);
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

                triggerDetectedPerChannel = new bool[GlobalInfo.NumChannel]; // 트리거 배열 초기화 추가
                Logger.Info($"Trigger Channel init complete"); // TEST

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
        public override string StartInterface()
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
                    foreach (PlcVariable item in ReadDevices)
                    {
                        item.ReadValue = readValues[i++];
                    }

                    // 0.8.7 : 트리거 신호만 On/Off 상태 저장 및 로그 추가
                    for (int k = 0; k < GlobalInfo.NumChannel; k++)
                    {
                        int ch = k + 1;

                        PlcVariable itemTrigger = null;
                        if (ch == 1)
                            itemTrigger = ReadDevices.Find(x => x.VarName == "TriggerAddress");
                        else
                            itemTrigger = ReadDevices.Find(x => x.VarName == $"TriggerAddress{ch}");

                        if (itemTrigger != null)
                        {
                            // 채널별 트리거 상태 관리
                            bool currentTriggerState = itemTrigger.ReadValue == 1;
                            bool previousTriggerState = triggerDetectedPerChannel[k];

                            if (currentTriggerState && !previousTriggerState)
                            {
                                triggerDetectedPerChannel[k] = true;
                                Logger.Debug($"Received Channel {ch} 'TRIGGER' Signal.");
                            }
                            else if (!currentTriggerState && previousTriggerState)
                            {
                                triggerDetectedPerChannel[k] = false;
                                Logger.Debug($"Received Channel {ch} 'TRIGGER_R' Signal.");
                            }
                        }
                    }

                    // 0.8.7 : Trigger On 시점에서 바코드 에러 체크 후 NG 신호 추가 및 바코드 정보 api 추가 전달
                    if (GlobalInfo.UseBarcodeModelMapping)
                    {
                        // TODO : Barcode COM별 CH 분기 (현재는 1PC 1Barcode 구조임)
                        var itemTriggerCh1 = ReadDevices.Find(x => x.VarName == "TriggerAddress");

                        if (itemTriggerCh1 != null)
                        {
                            bool currentTriggerState = itemTriggerCh1.ReadValue == 1;

                            // CH1 Trigger On 시점
                            if (triggerDetectedPerChannel[0] && !hasBarcodeProcessed)
                            {
                                Logger.Debug("Trigger Rising Edge detected - Processing barcode");
                                ProcessBarcodeOnTrigger();
                                hasBarcodeProcessed = true;
                            }
                            // CH1 트리거가 OFF로 변경되었을 때만 플래그 리셋
                            else if (!triggerDetectedPerChannel[0] && hasBarcodeProcessed)
                            {
                                hasBarcodeProcessed = false;
                                Logger.Debug("Trigger Falling Edge detected - Reset processing flag");
                            }
                        }
                    }

                    // 데이터 변경 시에만 HTTP 메시지 전송 (Task.Wait() 제거)
                    string currentRead = string.Join(" ", readValues);
                    if (PreviousRead != currentRead)
                    {
                        string json = CreateJsonMessage();

                        // Task.Wait() 제거 - Fire-and-Forget 패턴으로 변경
                        for (int j = 0; j < GlobalInfo.NumChannel; j++)
                        {
                            int channelIndex = j; // 클로저 문제 해결을 위한 지역 변수
                            Task.Run(() =>
                            {
                                try
                                {
                                    HttpMessage.SendHttpMessage(json, channelIndex + 1);
                                }
                                catch (Exception ex)
                                {
                                    Logger.Error($"HTTP Send Error on Channel {channelIndex + 1}: {ex.Message}");
                                }
                            });
                            Thread.Sleep(10);   // 이 부분 없으면 꼬임(TODO: 쓰레드 간 변수 독립성 보장 필요)
                        }
                    }

                    var itemReady = ReadDevices.Find(x => x.VarName == "ReadyAddress");
                    if(itemReady != null && itemReady.ReadValue == 0)
                    {
                        SetAPLCValueOn(itemReady.DeviceAddress);
                    }

                    // PreviousRead 업데이트 (중복 제거)
                    if (PreviousRead != currentRead)
                    {
                        PreviousRead = currentRead;
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

        private string CreateJsonMessage()
        {
            try
            {
                if (GlobalInfo.UseBarcodeModelMapping && BarcodeDataDevices.Any())
                {
                    var combinedData = ReadDevices.Concat(BarcodeDataDevices).ToList();
                    return JsonConvert.SerializeObject(combinedData);
                }
                else
                {
                    return JsonConvert.SerializeObject(ReadDevices);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"JSON serialization error: {ex.Message}");
                return JsonConvert.SerializeObject(ReadDevices); // 기본 데이터라도 전송
            }
        }


        const string MISSED_TEXT = "Missed";
        const int NORMAL_BARCODE_LENGTH = 21; // 신성델타 바코드 길이

        private void ProcessBarcodeOnTrigger()
        {
            Logger.Debug("Processing barcode on trigger activation");

            string _barcodeString = GlobalInfo.HoneywellBarcodeString;

            if (GlobalInfo.UseSinsungScenario)
            {
                _barcodeString = WaitForBarcodeUpdate(_barcodeString);
            }
            
            // 바코드 처리 로직
            bool hasModelMappedError = false; // model mapped error 미사용 (UI에서 진행)
            bool hasBarcodeMissed = false;

            // 누락 처리 (string.empty or 이전과 동일값)
            hasBarcodeMissed = CheckBarcodeMissed(_barcodeString);
            //if (hasBarcodeMissed) _cleanBarcode = MISSED_TEXT;

            // 정제
            string _cleanBarcode = CleanBarcodeString(_barcodeString);

            // 에러 체크
            bool hasBarcodeError = IsBarcodeError(_cleanBarcode);
            
            // 별도 리스트에 바코드 데이터 저장 (ReadDevices에 추가하지 않음)
            if (hasBarcodeMissed)
                UpdateBarcodeDataSeparately(MISSED_TEXT, hasBarcodeMissed, hasBarcodeError, hasModelMappedError);
            else
                UpdateBarcodeDataSeparately(_cleanBarcode, hasBarcodeMissed, hasBarcodeError, hasModelMappedError);

            if (hasBarcodeMissed || hasBarcodeError)
            {
                if (GlobalInfo.UseSinsungScenario) // 신성 델타 시나리오에서는 에러 어드레스 별도 ALARM
                    BarcodeAlarmBitOn_Sinsung();
                else
                    BarcodeAlarmBitOn(); // Error 시 기존 NG Address로 전달
            }

            prevBarcodeString = _cleanBarcode;
            GlobalInfo.HoneywellBarcodeString = string.Empty;
            Logger.Debug("Reset barcode data complete.");
        }

        private string CleanBarcodeString(string rawBarcode)
        {
            if (string.IsNullOrEmpty(rawBarcode))
                return string.Empty;

            // 0. 예시 케이스 추출 AJQ72913050KSD58E0170:00:100%:98:25/25:0.73:6.905:1:0:555/789:1
            string cleaned = rawBarcode.Split(':')[0];

            // 1. 앞뒤 공백 및 제어문자 제거
            cleaned = cleaned.Trim();

            // 2. 일반적인 제어문자 제거
            cleaned = cleaned.Trim('\0', '\r', '\n', '\t', '\b', '\f', '\v');

            // 3. 인쇄 불가능한 문자 제거
            cleaned = new string(cleaned.Where(c => !char.IsControl(c) || char.IsWhiteSpace(c)).ToArray());

            // 4. 연속된 공백을 하나로 변환 후 제거
            cleaned = Regex.Replace(cleaned, @"\s+", "").Trim();

            if (GlobalInfo.UseSinsungScenario)
            {
                cleaned = cleaned.Length >= 21 ? cleaned.Substring(0, 21) : cleaned;
            }

            return cleaned;
        }

        private void UpdateBarcodeDataSeparately(string cleanBarcode, bool hasBarcodeMissed, bool hasBarcodeError, bool hasModelMappedError)
        {
            // 기존 바코드 데이터 초기화
            BarcodeDataDevices.Clear();

            for (int j = 0; j < GlobalInfo.NumChannel; j++)
            {
                int channelNo = j + 1;

                BarcodeDataDevices.Add(new PlcVariable(0, "SerialNumber", cleanBarcode, channelNo));
                BarcodeDataDevices.Add(new PlcVariable(0, "hasBarcodeMissed", string.Empty, channelNo) { ReadValue = hasBarcodeMissed ? 1 : 0 });
                BarcodeDataDevices.Add(new PlcVariable(0, "hasBarcodeError", string.Empty, channelNo) { ReadValue = hasBarcodeError ? 1 : 0 });
                BarcodeDataDevices.Add(new PlcVariable(0, "hasModelMappedError", string.Empty, channelNo) { ReadValue = hasModelMappedError ? 1 : 0 });
            }

            Logger.Debug($"Barcode data updated separately.");
        }

        private string WaitForBarcodeUpdate(string initialBarcode)
        {
            int MAX_WAIT_ATTEMPTS = GlobalInfo.BarcodeWaitAttempts; // 최대 대기 횟수
            int waitAttempts = 0;
            string currentBarcode = initialBarcode;

            Logger.Debug($"Waiting for barcode update ({MAX_WAIT_ATTEMPTS * 0.1}s). Initial: {initialBarcode}");

            while (waitAttempts < MAX_WAIT_ATTEMPTS)  // 2초(100ms * 20)동안 barcode 추가 업데이트 진행
            {
                currentBarcode = GlobalInfo.HoneywellBarcodeString;
                Thread.Sleep(100);
                waitAttempts++;
            }

            Logger.Debug($"Barcode wait completed. Final: {currentBarcode}");
            return currentBarcode;
        }

        private bool CheckBarcodeMissed(string currentBarcode)
        {
            if (currentBarcode == prevBarcodeString || string.IsNullOrEmpty(currentBarcode))
            {
                Logger.Debug($"[NG] Barcode missed detection. Same as previous: {currentBarcode}");
                return true;
            }
            return false;
        }

        private bool IsBarcodeError(string barcodeString)
        {
           /*
               [정상]
               AJQ 7487 3872 KSD 58 J 0084
               AJQ74873869KSD58F0044
               AJQ74873867KSD58J0008
               AJQ72913048KSD58J0001
               AJQ72913048KSD58J0103
               AJQ74873869KSD58M0009
               AJQ74 87386 9KSD5 8M001 0

               [에러]
               J7836K
               ERROR
               ERROR::0%:0:0

               [에러지만 정상으로 변환 가능한 것]
               AJQ72913050KSD58E0170:00:100%:98:25/25:0.73:6.905:1:0:555/789:1
            */

            try
            {
                // 기본 검증
                if (string.IsNullOrEmpty(barcodeString))
                {
                    Logger.Debug($"[NG] Barcode error detected: Empty or null barcode string. : {barcodeString}");
                    return true;
                }

                // 명시적 에러 문자열 체크
                if (barcodeString.Contains("ERROR") ||
                    barcodeString.Contains("error") ||
                    barcodeString.Contains("FAIL"))
                {
                    Logger.Debug($"[NG] Barcode error detected: Error message found in barcode string. : {barcodeString}");
                    return true;
                }

                // 영숫자만 포함하는지 검증
                foreach (char c in barcodeString)
                {
                    if (!char.IsLetterOrDigit(c))
                    {
                        Logger.Debug($"[NG] Barcode error detected: Invalid character '{c}' found. : {barcodeString}");
                        return true;
                    }
                }

                if (GlobalInfo.UseSinsungScenario)
                {
                    // 길이 검증 (정상 바코드는 21자)
                    if (barcodeString.Length != NORMAL_BARCODE_LENGTH)
                    {
                        Logger.Debug($"[NG] Barcode error detected: Invalid length [{barcodeString.Length}], expected 21 characters. : {barcodeString}");
                        return true;
                    }

                    // 5. 정상 패턴 검증 (AJQ로 시작하는 패턴)
                    if (!barcodeString.StartsWith("AJQ"))
                    {
                        Logger.Debug($"[NG] Barcode error detected: Invalid prefix, expected 'AJQ'. : {barcodeString}");
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                Logger.Error($"Exception on {System.Reflection.MethodBase.GetCurrentMethod().Name} >>> {ex.Message}\r\n{ex.StackTrace}");
                return false;
            }
            finally { }
        }


        private void BarcodeAlarmBitOn()
        {
            try
            {
                bool isSetAlarm = false;

                // ch1
                var itemReady = ReadDevices.Find(x => x.VarName == "AnomalyOnAddress");
                if (itemReady != null && itemReady.ReadValue == 0 && itemReady.DeviceAddress != string.Empty)
                {
                    SetAPLCValueOn(itemReady.DeviceAddress);
                    isSetAlarm = true;
                }

                // ch2~4
                if (GlobalInfo.NumChannel > 1)
                {
                    for (int j = 1; j < GlobalInfo.NumChannel; j++)
                    {
                        itemReady = ReadDevices.Find(x => x.VarName == $"AnomalyOnAddress{j + 1}");
                        if (itemReady != null && itemReady.ReadValue == 0 && itemReady.DeviceAddress != string.Empty)
                        {
                            SetAPLCValueOn(itemReady.DeviceAddress);
                            isSetAlarm = true;
                        }
                    }
                }

                if (isSetAlarm)
                    Logger.Debug($"PLC write Success.");
                else
                    Logger.Debug($"Alarm occurs but not written to PLC (address is empty)");
            }
            catch (Exception ex)
            {
                Logger.Error($"Exception on {System.Reflection.MethodBase.GetCurrentMethod().Name} >>> {ex.Message}\r\n{ex.StackTrace}");
            }
            finally { }
        }

        private void BarcodeAlarmBitOn_Sinsung()
        {
            try
            {
                string BarcodeAlarmAddress1 = ConfigurationManager.AppSettings["BarcodeAlarmAddress1"] ?? string.Empty;
                string BarcodeAlarmAddress2 = ConfigurationManager.AppSettings["BarcodeAlarmAddress2"] ?? string.Empty;
                string BarcodeAlarmAddress3 = ConfigurationManager.AppSettings["BarcodeAlarmAddress3"] ?? string.Empty;
                string BarcodeAlarmAddress4 = ConfigurationManager.AppSettings["BarcodeAlarmAddress4"] ?? string.Empty;

                if (BarcodeAlarmAddress1 != string.Empty)
                {
                    SetAPLCValueOn(BarcodeAlarmAddress1);
                }
                if (BarcodeAlarmAddress2 != string.Empty)
                {
                    SetAPLCValueOn(BarcodeAlarmAddress2);
                }
                if (BarcodeAlarmAddress3 != string.Empty)
                {
                    SetAPLCValueOn(BarcodeAlarmAddress3);
                }
                if (BarcodeAlarmAddress4 != string.Empty)
                {
                    SetAPLCValueOn(BarcodeAlarmAddress4);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Exception on {System.Reflection.MethodBase.GetCurrentMethod().Name} >>> {ex.Message}\r\n{ex.StackTrace}");
            }
            finally { }
        }

        static int ApplyBarcodeModelMapping(string barcodeString)
        {
            int modelNumber = 0;

            foreach (string modelString in GlobalInfo.BarcodeModelDatas)
            {
                modelNumber++;

                if (barcodeString.Contains(modelString))
                {
                    if (modelString != prevBarcodeString && modelString != string.Empty)
                        Logger.Debug($"The model is changed. [prev model] : {prevBarcodeString} -> [now model] {modelString} (Model{modelNumber})");
                    prevBarcodeString = modelString;
                    return modelNumber;
                }
            }

            //Logger.Debug($"The read barcode value ​​do not match. value : {GlobalInfo.HoneywellBarcodeString}");
            return 0;
        }

        public void SetReadValueForModel(int modelNumber, List<PlcVariable> ReadDevices)
        {
            string modelName = $"Model{modelNumber}";

            foreach (PlcVariable item in ReadDevices)
            {
                if (item.ChannelNo >= 1 && item.ChannelNo <= 4 && item.VarName.Contains("Model"))
                {
                    if (item.VarName == modelName)
                    {
                        item.ReadValue = 1;
                    }
                    else
                        item.ReadValue = 0;
                }
            }
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
                if (device == "" || device == string.Empty)
                {
                    Logger.Debug($"PLC Random Block Read : No Device");
                    return res;
                }
                lock (aut)
                {
                    for (int i = 0; i < RetryNumLimit; i++)
                    {
                        //Logger.Debug("Start ReadDevice Random");
                        obj = aut.ReadDeviceRandom(device, size, out values[0]);
                        //Logger.Debug("End ReadDevice Random");
                        if (Convert.ToInt32(obj) == 0)
                        {
                            //Logger.Debug($"PLC Random Block Read Success - device:{device.Replace("\n", "/")}, size:{size}, value:{values[0]}");
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
            if (device == string.Empty)
            {
                //Logger.Error("Write Device is empty");
                return -1;
            }

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

        // SPY TEST
        public override int WriteToPLCRandomString(string device, string text)
        {
            int res = 0;
            try
            {
                WriteDeviceBlock(device, text);
            }
            catch (Exception ex)
            {
                Logger.Error($"Exception on {System.Reflection.MethodBase.GetCurrentMethod().Name} >>> {ex.Message}\r\n{ex.StackTrace}");
                res = -1;
            }
            finally { }

            return res;
        }
        public override string ReadFromPLCRandomString(string device, int size)
        {
            string sRead = "";
            try
            {
                lock (aut)
                {
                    //블럭으로 읽을때 
                    ReadDeviceBlock(device, size, out sRead);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Exception on {System.Reflection.MethodBase.GetCurrentMethod().Name} >>> {ex.Message}\r\n{ex.StackTrace}");
            }
            finally { }
            return sRead;
        }

        private Object thisLock = new Object();
        short[] sInt = new short[100];

        public bool WriteDeviceBlock(string _sAdd, string _str)
        {
            if (_str == "")
            {
                sInt = new short[10];
                Array.Clear(sInt, 0, sInt.Length);
                bool bCheck1 = WriteDeviceBlock2(_sAdd, sInt.Length, ref sInt[0]);
                return bCheck1;
            }
            string str = _str;
            string[] str_temp;

            if (str.Length % 2 == 0)
            {
                str_temp = new string[str.Length / 2];
                sInt = new short[str_temp.Length];

                for (int i = 0; i < str.Length / 2; i++)
                {
                    str_temp[i] = str.Substring(i * 2, 2);
                }

                for (int i = 0; i < str_temp.Length; i++)
                {
                    byte[] bytes = Encoding.ASCII.GetBytes(str_temp[i]);
                    short sh = BitConverter.ToInt16(bytes, 0);
                    sInt[i] = sh;
                }
            }
            else
            {
                str_temp = new string[(str.Length / 2) + 1];
                sInt = new short[str_temp.Length];

                for (int i = 0; i < str.Length / 2 + 1; i++)
                {
                    if (i < (str.Length - 1) / 2)
                        str_temp[i] = str.Substring(i * 2, 2);
                    else
                        str_temp[i] = str.Substring(i * 2, 1);
                }

                for (int i = 0; i < str_temp.Length; i++)
                {
                    if (i < str_temp.Length - 1)
                    {
                        byte[] bytes = Encoding.ASCII.GetBytes(str_temp[i]);
                        short sh = BitConverter.ToInt16(bytes, 0);
                        sInt[i] = sh;
                    }
                    else
                    {
                        char data = Convert.ToChar(str_temp[i].Substring(0, 1));
                        sInt[i] = (short)data;
                    }
                }
            }

            bool bCheck2 = WriteDeviceBlock2(_sAdd, sInt.Length, ref sInt[0]);
            return bCheck2;
        }
        public bool WriteDeviceBlock2(string szDevice, int iSize, ref short iData)
        {
            int iRst = -1;

            lock (thisLock)
            {
                try
                {
                    iRst = aut.WriteDeviceBlock2(szDevice, iSize, ref iData);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
            if (iRst == 0) return true;
            return false;
        }
        public bool ReadDeviceBlock(string _sAdd, int size, out string _str)
        {
            sInt = new short[size];
            Array.Clear(sInt, 0, sInt.Length);
            bool bCheck = ReadDeviceBlock2(_sAdd, sInt.Length, out sInt[0]);

            _str = "";
            if (bCheck)
            {
                for (int i = 0; i < sInt.Length; i++)
                {
                    byte[] bytes = BitConverter.GetBytes(sInt[i]);
                    _str += Encoding.Default.GetString(bytes);
                }
            }

            return bCheck;
        }
        public bool ReadDeviceBlock2(string szDevice, int iSize, out short lplData)
        {
            lplData = 0;


            int iRst = -1;

            lock (thisLock)
            {
                try
                {
                    iRst = aut.ReadDeviceBlock2(szDevice, iSize, out lplData);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
            if (iRst == 0) return true;

            return false;
        }

        public override int GetReadDevicesCount()
        {
            return ReadDevices.Count;
        }
    }
}
