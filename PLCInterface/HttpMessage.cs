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
        public static int UiHttpPort5 { get; set; } = Convert.ToInt32(ConfigurationManager.AppSettings["HttpSendPort5"] ?? "6165");
        public static int UiHttpPort6 { get; set; } = Convert.ToInt32(ConfigurationManager.AppSettings["HttpSendPort6"] ?? "6166");

        /// <summary>
        /// UI 애플리케이션으로 HTTP POST 메시지를 전송하는 메서드
        /// 개선사항: 타임아웃 설정, 예외 처리 강화, 리소스 관리 개선
        /// </summary>
        /// <param name="messageBodyJson">전송할 JSON 메시지 본문</param>
        /// <param name="ch">채널 번호 (1~6)</param>
        public static void SendHttpMessage(string messageBodyJson, int ch=1)
        {
            HttpWebRequest httpWebRequest = null;   // HTTP 요청 객체 (안전한 해제를 위해 null 초기화)
            HttpWebResponse httpResponse = null;    // HTTP 응답 객체 (안전한 해제를 위해 null 초기화)
            
            try
            {
                //Logger.Info(messageBodyJson); // 디버깅용 메시지 로깅 (필요시 활성화)

                // 채널별 포트 번호 결정
                int nPort = UiHttpPort1;
                if (ch == 2)
                    nPort = UiHttpPort2;
                else if (ch == 3)
                    nPort = UiHttpPort3;
                else if (ch == 4)
                    nPort = UiHttpPort4;
                else if (ch == 5)
                    nPort = UiHttpPort5;
                else if (ch == 6)
                    nPort = UiHttpPort6;

                // 로컬호스트 URL 생성 (127.0.0.1 사용으로 네트워크 오버헤드 최소화)
                string url = $"http://127.0.0.1:{nPort}/";
                //Logger.Info($"Http Send url : {url}"); // URL 로깅 (필요시 활성화)
                
                // HTTP 요청 객체 생성
                httpWebRequest = (HttpWebRequest)WebRequest.Create(url);

                // 실시간 통신을 위한 최적화된 타임아웃 설정
                httpWebRequest.Timeout = 2000;          // 연결 타임아웃: 2초 
                httpWebRequest.ReadWriteTimeout = 5000;  // 읽기/쓰기 타임아웃: 5초 
                
                // TCP 연결 최적화 설정
                httpWebRequest.KeepAlive = false;        // 연결 재사용 비활성화 (빠른 해제)
                httpWebRequest.ServicePoint.Expect100Continue = false; // 100-Continue 헤더 비활성화
                httpWebRequest.ServicePoint.UseNagleAlgorithm = false;  // Nagle 알고리즘 비활성화 (지연 최소화)
                
                // HTTP 헤더 설정
                httpWebRequest.ContentType = "application/json";  // JSON 형식 명시
                httpWebRequest.Method = "POST";                   // POST 메서드 사용

                // 요청 본문에 JSON 데이터 작성
                using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
                {
                    streamWriter.Write(messageBodyJson);
                }

                // HTTP 응답 수신 및 처리
                httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
                using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    var result = streamReader.ReadToEnd();
                    Console.WriteLine(result.ToString()); // 응답 내용 콘솔 출력
                }
                
                // 성공 플래그 설정을 별도 메서드로 분리 (코드 중복 제거)
                SetSuccessFlag(ch, true);
            }
            // WebException 별도 처리 (네트워크 관련 예외 상세 정보 제공)
            catch (WebException webEx)
            {
                // 웹 관련 예외 상세 정보 수집
                string errorDetail = $"WebException on Channel {ch} - Status: {webEx.Status}, Message: {webEx.Message}";
                
                // HTTP 응답이 있는 경우 상태 코드 추가 정보 수집
                if (webEx.Response != null)
                {
                    try
                    {
                        var errorResponse = (HttpWebResponse)webEx.Response;
                        errorDetail += $", StatusCode: {errorResponse.StatusCode}";
                    }
                    catch { /* 응답 정보 수집 실패 시 무시 */ }
                }
                
                Logger.Error($"{errorDetail}\r\n{webEx.StackTrace}");
                SetSuccessFlag(ch, false);
            }
            // 일반 예외 처리 개선 (채널 정보 포함)
            catch (Exception ex)
            {
                Logger.Error($"Channel {ch} HTTP Error: {ex.Message}\r\n{ex.StackTrace}");
                SetSuccessFlag(ch, false);
            }
            // 안전한 리소스 해제 (메모리 누수 방지)
            finally 
            {
                // HTTP 응답 객체 안전 해제
                try
                {
                    httpResponse?.Close();
                }
                catch { /* 해제 실패 시 무시 (이미 해제되었거나 null인 경우) */ }
                
                // HTTP 요청 객체 안전 해제
                try
                {
                    httpWebRequest?.Abort();
                }
                catch { /* 해제 실패 시 무시 (이미 해제되었거나 null인 경우) */ }
            }
        }

        public static string PostRequest(string url, string json, int timeoutMilliseconds = 0)
        {
            string receivedHeader = string.Empty;
            try
            {
                var httpWebRequest = (HttpWebRequest)WebRequest.Create(url);
                httpWebRequest.ContentType = "application/json; charset=utf-8";
                httpWebRequest.Method = "POST";
                if (timeoutMilliseconds > 0)
                    httpWebRequest.Timeout = timeoutMilliseconds; // 타임아웃 설정

                byte[] byteArray = Encoding.UTF8.GetBytes(json);
                httpWebRequest.ContentLength = byteArray.Length;

                Stream datastream = httpWebRequest.GetRequestStream();
                datastream.Write(byteArray, 0, byteArray.Length);
                datastream.Close();
                
                var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
                using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    var result = streamReader.ReadToEnd();
                    receivedHeader = result;
                }
            }
            catch (WebException ex) when (ex.Status == WebExceptionStatus.Timeout)
            {
                Logger.Error($"Request timed out: {System.Reflection.MethodBase.GetCurrentMethod().Name}");
                receivedHeader = "timeout_error";
            }
            catch (Exception ex)
            {
                Logger.Error($"Exception on {System.Reflection.MethodBase.GetCurrentMethod().Name} >>> {ex.Message}\r\n{ex.StackTrace}");
                receivedHeader = $"error: {ex.Message}";
            }
            return receivedHeader;
        }

        /// <summary>
        /// 채널별 HTTP 전송 성공/실패 플래그를 설정하는 메서드
        /// 개선사항: 중복 코드 제거를 위해 별도 메서드로 분리
        /// </summary>
        /// <param name="ch">채널 번호 (1~6)</param>
        /// <param name="success">성공 여부 (true: 성공, false: 실패)</param>
        private static void SetSuccessFlag(int ch, bool success)
        {
            // 채널별 성공 플래그 설정 (기존 레거시 방식 유지)
            if (ch > 0 && ch <= GlobalInfo.NumChannel) 
                GlobalInfo.IsSuccessToSend[ch-1] = success;
        }
    }
}
