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
        public static int UiHttpPort { get; set; } = Convert.ToInt32(ConfigurationManager.AppSettings["HttpSendPort"] ?? "6161");
        public static bool IsSuccessToSend { get; set; } = false;

        public static void SendHttpMessage(string messageBodyJson)
        {
            try
            {
                Logger.Info(messageBodyJson);

                string url = $"http://localhost:{UiHttpPort}/";
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
                IsSuccessToSend = true;
            }
            catch (Exception ex)
            {
                Logger.Error($"{ex.Message}\r\n{ex.StackTrace}");
                IsSuccessToSend = false;
            }
            finally { }
        }
    }
}
