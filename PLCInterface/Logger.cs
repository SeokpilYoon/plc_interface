using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLCInterface
{
    public enum LogLevel
    {
        ALL = 0,
        TRACE = 1,
        DEBUG = 2,
        INFO = 3,
        WARN = 4,
        ERROR = 5,
        FATAL = 6,
        OFF = 7
    }

    public static class Logger
    {
        private static readonly object logLock = new object();
        private static readonly object dataLock = new object();

        public static LogLevel LogLevel { get; set; } = LogLevel.ALL;

        public static string logRoot = ConfigurationManager.AppSettings["AppRoot"] ?? "C:/Anomaly_Detection/";
        public static bool isLocal = false;

        private static void AddLog(string logLevel, string logMessage)
        {
            try
            {
                string logFolder;
                if (isLocal)
                {
                    logFolder = $"./log/{DateTime.Now.ToString("yyyyMMdd")}";
                }
                else
                {
                    logFolder = $"{logRoot}PlcInterfaceLog/{DateTime.Now.ToString("yyyyMMdd")}";
                }
                if (!Directory.Exists(logFolder)) Directory.CreateDirectory(logFolder);
                string fileName = logFolder + "\\" + "Log_" + DateTime.Now.ToString("yyyyMMdd") + ".log";

                DateTime dtm = DateTime.Now;
                string formatDateTime = string.Format("[{0:0000}/{1:00}/{2:00} {3:00}:{4:00}:{5:00}.{6:000}] [{7}] ",
                    dtm.Year, dtm.Month, dtm.Day, dtm.Hour, dtm.Minute, dtm.Second, dtm.Millisecond, logLevel);
                string logLine = formatDateTime + logMessage;

                lock (logLock)
                {
                    var logAdapter = File.AppendText(fileName);
                    logAdapter.WriteLine(logLine);
                    logAdapter.Close();
                }
            }
            catch (Exception ex)
            {
                AddLogEvent(ex);
            }
            finally
            {
                Console.WriteLine(logMessage);
            }
        }

        private static void AddLogEvent(Exception ex)
        {
            string logFolder;
            if (isLocal)
            {
                logFolder = $"./log/{DateTime.Now.ToString("yyyyMMdd")}";
            }
            else
            {
                logFolder = $"{logRoot}log/{DateTime.Now.ToString("yyyyMMdd")}";
            }
            if (!Directory.Exists(logFolder)) Directory.CreateDirectory(logFolder);
            string fileName = logFolder + "\\" + "Log_" + DateTime.Now.ToString("yyyyMMdd") + ".log";

            DateTime dtm = DateTime.Now;
            string formatDateTime = string.Format("[{0:0000}/{1:00}/{2:00} {3:00}:{4:00}:{5:00}.{6:000}] [LOG_EVENT] ",
                dtm.Year, dtm.Month, dtm.Day, dtm.Hour, dtm.Minute, dtm.Second, dtm.Millisecond);
            string logLine = $"{formatDateTime}{ex.Message}\r\n{ex.StackTrace}";

            lock (logLock)
            {
                var logAdapter = File.AppendText(fileName);
                logAdapter.WriteLine(logLine);
                logAdapter.Close();
            }
        }
        public static void Trace(string logMessage)
        {
            if (LogLevel <= LogLevel.TRACE)
            {
                AddLog("TRACE", logMessage);
            }
        }

        public static void Debug(string logMessage)
        {
            if (LogLevel <= LogLevel.DEBUG)
            {
                AddLog("DEBUG", logMessage);
            }
        }

        public static void Info(string logMessage)
        {
            if (LogLevel <= LogLevel.INFO)
            {
                AddLog("INFO", logMessage);
            }
        }

        public static void Warn(string logMessage)
        {
            if (LogLevel <= LogLevel.WARN)
            {
                AddLog("WARN", logMessage);
            }
        }

        public static void Error(string logMessage)
        {
            if (LogLevel <= LogLevel.ERROR)
            {
                AddLog("ERROR", logMessage);
            }
        }

        public static void Fatal(string logMessage)
        {
            if (LogLevel <= LogLevel.FATAL)
            {
                AddLog("FATAL", logMessage);
            }
        }

        private static void DataLog(string logMessage)
        {
            //Console.ForegroundColor = ConsoleColor.White;

            try
            {
                string logFolder;
                if (isLocal)
                {
                    logFolder = $"./log/{DateTime.Now.ToString("yyyyMMdd")}";
                }
                else
                {
                    logFolder = $"{logRoot}log/{DateTime.Now.ToString("yyyyMMdd")}";
                }
                if (!Directory.Exists(logFolder)) Directory.CreateDirectory(logFolder);
                string fileName = logFolder + "\\" + "Data.log";

                lock (dataLock)
                {
                    var logAdapter = File.AppendText(fileName);
                    logAdapter.WriteLine(logMessage);
                    logAdapter.Close();
                }
            }
            catch (Exception ex)
            {
                AddLogEvent(ex);
            }
            finally
            {
                Console.WriteLine(logMessage);
            }
        }
    }
}
