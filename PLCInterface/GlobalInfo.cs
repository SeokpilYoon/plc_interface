using System;
using System.Collections.Generic;
using System.Configuration;

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

        public static bool InitializeGlobalInfo()
        {
            bool isSuccess = true;
            try
            {
                NumChannel = Convert.ToInt32(ConfigurationManager.AppSettings["NumChannel"] ?? "1");
                EnableChannels = new bool[NumChannel];
                for (int i = 0; i < NumChannel; i++)
                {
                    EnableChannels[i] = (ConfigurationManager.AppSettings[$"EnableChannel{i}"] ?? "FALSE").ToUpper().Equals("TRUE");
                    if (EnableChannels[i] == true)
                        NumEnabledChannel++;
                }

                UseDongaScenario = (ConfigurationManager.AppSettings["UseDongaScenario"] ?? "FALSE").ToUpper().Equals("TRUE");
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
