using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace PLCInterface
{
    static class HttpMessage
    {
        public static int UiHttpPort1 { get; set; } = Convert.ToInt32(ConfigurationManager.AppSettings["HttpSendPort"] ?? "6161");
        public static int UiHttpPort2 { get; set; } = Convert.ToInt32(ConfigurationManager.AppSettings["HttpSendPort2"] ?? "6162");
        public static int UiHttpPort3 { get; set; } = Convert.ToInt32(ConfigurationManager.AppSettings["HttpSendPort3"] ?? "6163");
        public static int UiHttpPort4 { get; set; } = Convert.ToInt32(ConfigurationManager.AppSettings["HttpSendPort4"] ?? "6164");
        public static bool IsSuccessToSend1 { get; set; } = false;
        public static bool IsSuccessToSend2 { get; set; } = false;
        public static bool IsSuccessToSend3 { get; set; } = false;
        public static bool IsSuccessToSend4 { get; set; } = false;

        public static void SendHttpMessage(string messageBodyJson, int ch=1)
        {
            try
            {
                Logger.Info(messageBodyJson);

                int nPort = UiHttpPort1;
                if (ch == 2)
                    nPort = UiHttpPort2;
                else if (ch == 3)
                    nPort = UiHttpPort3;
                else if (ch == 4)
                    nPort = UiHttpPort4;

                string url = $"http://localhost:{nPort}/";
                Logger.Info($"Http Send url : {url}");
                var httpWebRequest = (HttpWebRequest)WebRequest.Create(url);

                httpWebRequest.ContentType = "application/json";
                httpWebRequest.Method = "POST";

                using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
                {
                    streamWriter.Write(messageBodyJson);
                }

                var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
                using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    var result = streamReader.ReadToEnd();
                    Console.WriteLine(result.ToString());
                }
                if (ch == 1)
                    IsSuccessToSend1 = true;
                else if (ch == 2)
                    IsSuccessToSend2 = true;
                else if (ch == 3)
                    IsSuccessToSend3 = true;
                else if (ch == 4)
                    IsSuccessToSend4 = true;
                httpWebRequest.Abort();
                httpResponse.Close();
            }
            catch (Exception ex)
            {
                Logger.Error($"{ex.Message}\r\n{ex.StackTrace}");
                if (ch == 1)
                    IsSuccessToSend1 = false;
                else if (ch == 2)
                    IsSuccessToSend2 = false;
                else if (ch == 3)
                    IsSuccessToSend3 = false;
                else if (ch == 4)
                    IsSuccessToSend4 = false;
            }
            finally { }
        }
    }
}
