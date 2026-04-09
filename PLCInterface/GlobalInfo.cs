using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Windows.Input;

namespace PLCInterface
{
    static class GlobalInfo
    {
        public static string ProgramVersion { get; set; }

        public static int NumChannel { get; set; } = 1;
        public static bool[] EnableChannels;
        public static int NumEnabledChannel { get; set; } = 0;

        // 동아엘텍
        public static bool UseDongaScenario { get; set; } = false;
        public static string DongaDataPath { get; set; } = @"D:/DONGA/";
        public static string DongaTriggerTime { get; set; } = string.Empty;
        public static bool UseAuroraTowerLamp { get; set; } = false;
        public static bool USBLamp_AlarmBuzzer { get; set; } = false;
        public static bool USBLamp_AlarmRed { get; set; } = false;
        public static bool USBLamp_AlarmYellow { get; set; } = false;

        // Oracle DB 연동
        public static bool UseOracleDB { get; set; } = false;
        public static string OracleIP { get; set; } = string.Empty;
        public static string OraclePort { get; set; } = string.Empty;
        public static string OracleServiceName { get; set; } = string.Empty;
        public static string OracleUserID { get; set; } = string.Empty;
        public static string OraclePassword { get; set; } = string.Empty;
        public static string OracleTable { get; set; } = string.Empty;
        public static string OracleColumn_LINE_CD { get; set; } = string.Empty;
        public static List<DongaResult> DongaResults;

        public static string LSElectricIP { get; set; } = "127.0.0.1";
        public static int LSElectricPort { get; set; } = 1;
        public static bool UseXgbPlcType { get; set; } = false;  // 0.8.6 : LS산전 PLC 타입 Config 추가

        public static bool UseLGDVHCOFScenario { get; set; } = false;
        public static int ConsecutiveAlarm { get; set; } = 1;
        public static int ConsecutiveImage { get; set; } = 1;

        // Honeywell BarcodeReader
        public static bool UseHoneywellBarcodeReader { get; set; } = false;
        public static string HoneywellBarcodePort { get; set; } = "COM1";
        public static string HoneywellBarcodeString { get; set; } = string.Empty;

        // 바코드 string별 모델 매핑 기능 
        public static bool UseBarcodeModelMapping { get; set; } = false;
        public static int BarcodeModelCount { get; set; } = 1;

        public static List<string> BarcodeModelDatas = new List<string>();
        //public static string BarcodeModel1 { get; set; } = string.Empty; // UseBarcodeModelMapping = true 시 생성된 만큼 읽어옴
        //public static string BarcodeModel2 { get; set; } = string.Empty;
        public static bool UseSinsungScenario { get; set; } = false; // 0.8.7 : 신성델타 시나리오 추가
        public static int BarcodeWaitAttempts { get; set; } = 20; // 0.8.7 : 신성델타 시나리오 추가

        public static bool SkipPLCInterface { get; set; } = false;
        public static int UiHttpPort1 { get; set; } = 6161;
        public static List<bool> IsSuccessToSend { get; set; } = new List<bool>();
        public static bool IsConnectedToMES { get; set; } = false;

        public static bool InitializeGlobalInfo()
        {
            bool isSuccess = true;
            try
            {
                NumChannel = Convert.ToInt32(ConfigurationManager.AppSettings["NumChannel"] ?? "1");
                EnableChannels = new bool[NumChannel];
                for (int i = 0; i < NumChannel; i++)
                {
                    EnableChannels[i] = (ConfigurationManager.AppSettings[$"EnableChannel{i + 1}"] ?? "FALSE").ToUpper().Equals("TRUE");
                    if (EnableChannels[i] == true)
                        NumEnabledChannel++;
                }

                UseDongaScenario = (ConfigurationManager.AppSettings["UseDongaScenario"] ?? "FALSE").ToUpper().Equals("TRUE");
                DongaDataPath = ConfigurationManager.AppSettings["DongaDataPath"] ?? string.Empty;
                UseAuroraTowerLamp = (ConfigurationManager.AppSettings["UseAuroraTowerLamp"] ?? "FALSE").ToUpper().Equals("TRUE");
                USBLamp_AlarmBuzzer = (ConfigurationManager.AppSettings["USBLamp_AlarmBuzzer"] ?? "FALSE").ToUpper().Equals("TRUE");
                USBLamp_AlarmRed = (ConfigurationManager.AppSettings["USBLamp_AlarmRed"] ?? "FALSE").ToUpper().Equals("TRUE");
                USBLamp_AlarmYellow = (ConfigurationManager.AppSettings["USBLamp_AlarmYellow"] ?? "FALSE").ToUpper().Equals("TRUE");

                UseOracleDB = (ConfigurationManager.AppSettings["UseOracleDB"] ?? "FALSE").ToUpper().Equals("TRUE");
                OracleIP = ConfigurationManager.AppSettings["OracleIP"] ?? string.Empty;
                OraclePort = ConfigurationManager.AppSettings["OraclePort"] ?? string.Empty;
                OracleServiceName = ConfigurationManager.AppSettings["OracleServiceName"] ?? string.Empty;
                OracleUserID = ConfigurationManager.AppSettings["OracleUserID"] ?? string.Empty;
                OraclePassword = ConfigurationManager.AppSettings["OraclePassword"] ?? string.Empty;
                OracleTable = ConfigurationManager.AppSettings["OracleTable"] ?? string.Empty;
                OracleColumn_LINE_CD = ConfigurationManager.AppSettings["OracleColumn_LINE_CD"] ?? string.Empty;
                DongaResults = new List<DongaResult>();

                // Donga 폴더 추가
                if (UseDongaScenario)
                {
                    Utility.MakeFolder($"{DongaDataPath}");
                }

                LSElectricIP = ConfigurationManager.AppSettings["LSElectricIP"] ?? "127.0.0.1";
                LSElectricPort = Convert.ToInt32(ConfigurationManager.AppSettings["LSElectricPort"] ?? "1");
                UseXgbPlcType = (ConfigurationManager.AppSettings["UseXgbPlcType"] ?? "FALSE").ToUpper().Equals("TRUE");

                UseLGDVHCOFScenario = (ConfigurationManager.AppSettings["UseLGDVHCOFScenario"] ?? "FALSE").ToUpper().Equals("TRUE");
                ConsecutiveAlarm = Convert.ToInt32(ConfigurationManager.AppSettings["ConsecutiveAlarm"] ?? "1");
                ConsecutiveImage = Convert.ToInt32(ConfigurationManager.AppSettings["ConsecutiveImage"] ?? "1");

                UseHoneywellBarcodeReader = (ConfigurationManager.AppSettings["UseHoneywellBarcodeReader"] ?? "FALSE").ToUpper().Equals("TRUE");
                HoneywellBarcodePort = ConfigurationManager.AppSettings["HoneywellBarcodePort"] ?? "COM1";

                // 0.8.5 : Barcode 값과 Model 정보 Mapping 기능 추가
                UseBarcodeModelMapping = (ConfigurationManager.AppSettings["UseBarcodeModelMapping"] ?? "FALSE").ToUpper().Equals("TRUE");
                BarcodeModelCount = Convert.ToInt32(ConfigurationManager.AppSettings["BarcodeModelCount"] ?? "1");

                if (UseBarcodeModelMapping)
                {
                    for (int i = 0; i < BarcodeModelCount; i++)
                    {
                        // 문자열 값을 읽어와 리스트에 추가
                        string _tmp = ConfigurationManager.AppSettings[$"BarcodeModel{i + 1}"] ?? string.Empty;
                        BarcodeModelDatas.Add(_tmp);
                    }
                }

                UseSinsungScenario = (ConfigurationManager.AppSettings["UseSinsungScenario"] ?? "FALSE").ToUpper().Equals("TRUE"); // 0.8.7 : 신성델타 시나리오 추가
                BarcodeWaitAttempts = Convert.ToInt32(ConfigurationManager.AppSettings["BarcodeWaitAttempts"] ?? "20");
                UiHttpPort1 = Convert.ToInt32(ConfigurationManager.AppSettings["HttpSendPort"] ?? "6161");
                SkipPLCInterface = (ConfigurationManager.AppSettings["SkipPLCInterface"] ?? "FALSE").ToUpper().Equals("TRUE");

                for (int i = 0; i < NumChannel; i++)
                    IsSuccessToSend.Add(false);
            }
            catch (Exception ex)
            {
                Logger.Error($"Exception on {System.Reflection.MethodBase.GetCurrentMethod().Name} >>> {ex.Message}\r\n{ex.StackTrace}");
                isSuccess = false;
            }
            finally
            {
            }

            return isSuccess;
        }
    }
}