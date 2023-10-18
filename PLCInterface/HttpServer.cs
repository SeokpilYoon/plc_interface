using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PLCInterface
{
    public class USBLamp
    {
        public bool Red { get; set; }
        public bool Yellow { get; set; }
        public bool Green { get; set; }
        public bool Buzzer { get; set; }
        public bool Blink { get; set; }
    }
    public class DongaResult
    {
        public int CHANNEL { get; set; }
        public string PART_NO { get; set; }
        public string LINE_CD { get; set; }
        public string TEST_DT { get; set; }
        public string OK_YN { get; set; }
        public string IMAGEPATH { get; set; }
        public string CAMERA_NO { get; set; }
    }

    public class HttpServer
    {
        PlcVariable writeDevice; // = new PlcVariable();
        
        private string bindingAddress = string.Empty;  // = "http://*:6161/";

        private HttpListener listener = null;
        private Thread listenThread = null;

        bool UseBusy = false;
        bool AnomalyAutoOff = false;
        bool UseLGDTMGlassScenario = false;
        int[] results;

        public HttpServer()
        {
            //TraceManager.webRoot = (ConfigurationManager.AppSettings["AppFolder"] ?? @"D:/anomaly_detection/") + "LOG/";
            bindingAddress = "http://*:" + (ConfigurationManager.AppSettings["HttpServicePort"] ?? "6262") + "/";
            Logger.Info($"HTTP Server Binding Address is set to {bindingAddress}");

            UseBusy = (ConfigurationManager.AppSettings["UseBusy"] ?? string.Empty).ToUpper().Equals("TRUE");
            AnomalyAutoOff = (ConfigurationManager.AppSettings["AnomalyAutoOff"] ?? string.Empty).ToUpper().Equals("TRUE");
            UseLGDTMGlassScenario = (ConfigurationManager.AppSettings["UseLGDTMGlassScenario"] ?? string.Empty).ToUpper().Equals("TRUE");

            results = new int[GlobalInfo.NumChannel];
        }

        /// <summary>
        /// PieIf에서 보낸 메시지를 처리하기 위한 웹서버
        /// </summary>
        public void ActivateHttpService()
        {
            try
            {
                #region HttpListenerBased
                listener = new HttpListener();
                listener.Prefixes.Add(bindingAddress);
                listener.Start();

                listenThread = new Thread(new ThreadStart(ListenHttpRequest))
                {
                    IsBackground = true
                };
                listenThread.Start();
                #endregion HttpListenerBased
            }
            catch (Exception ex)
            {
                Logger.Error($"{ex.Message}\r\n{ex.StackTrace}");
            }

            #region SocketBased

            //IPEndPoint ipep = new IPEndPoint(IPAddress.Any, 6161);
            //Socket server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            //server.Bind(ipep);
            //server.Listen(20);
            //while (true)
            //{
            //    Socket client = server.Accept();
            //    try
            //    {
            //        String file = Recieve(client);
            //        //FileInfo FI = new FileInfo(file);
            //        //client.Send(Header(client, FI));
            //    }
            //    finally
            //    {
            //        client.Close();
            //    }
            //}    

            #endregion SocketBased
        }

        private void ListenHttpRequest()
        {
            try
            {
                //CommonInterface plc = new MelsecInterface();
                CommonInterface plc;
                byte plcType = Convert.ToByte(ConfigurationManager.AppSettings["PlcType"] ?? "1");
                if (plcType == 1)
                    plc = new MelsecInterface();
                else if (plcType == 2)
                    plc = new ModbusInterface();
                else if (plcType == 3)
                    plc = new AdvantechDAQInterface();
                else if (plcType == 4)
                    plc = new ADLinkDIOInterface(); // LGD에 3으로 배포되었으나 4로 변경
                else
                    plc = new MelsecInterface();

                plc.StartInterface();
                
                /*if (GlobalInfo.UseAuroraTowerLamp == true)
                {
                    AuroraUSBTowerLampInterface auroraUSBTowerLampInterface = new AuroraUSBTowerLampInterface();
                    auroraUSBTowerLampInterface.StartInterface();
                }*/

                while (true)
                {
                    HttpListenerContext context = listener.GetContext();
                    var request = context.Request;
                    string jsonText = "";
                    string msgTitle = "";
                    using (var reader = new StreamReader(request.InputStream,
                                                         request.ContentEncoding))
                    {
                        jsonText = reader.ReadToEnd();
                        Logger.Trace($"{jsonText}");
                        try
                        {
                            /*if (request.Url.LocalPath == "/USBLamp")
                            {
                                USBLamp uSBLamp = JsonConvert.DeserializeObject<USBLamp>(jsonText);
                                if (GlobalInfo.UseAuroraTowerLamp == true)
                                    auroraUSBTowerLampInterface.SetLed(uSBLamp.Red, uSBLamp.Yellow, uSBLamp.Green, uSBLamp.Buzzer, uSBLamp.Blink);
                            }
                            else*/
                            if (request.Url.LocalPath == "/Donga_Result")
                            {
                                if (GlobalInfo.UseOracleDB == true && GlobalInfo.UseDongaScenario == true)
                                {
                                    DongaResult dongaResult = JsonConvert.DeserializeObject<DongaResult>(jsonText);
                                    GlobalInfo.DongaResults.Add(dongaResult);
                                    //Utility.SendOracleDB(dongaResult.PART_NO, dongaResult.LINE_CD, dongaResult.TEST_DT, dongaResult.OK_YN, dongaResult.IMAGEPATH, dongaResult.CAMERA_NO);
                                }
                            }
                            else
                            {
                                string writeDeviceOn = ConfigurationManager.AppSettings["AnomalyOnAddress"] ?? string.Empty;
                                string writeDeviceOff = ConfigurationManager.AppSettings["AnomalyOffAddress"] ?? string.Empty;
                                string writeDeviceCapture = ConfigurationManager.AppSettings["CaptureCompleteAddress"] ?? string.Empty;
                                string writeDeviceBusy = ConfigurationManager.AppSettings["BusyAddress"] ?? string.Empty;
                                string writeDeviceModelChanged = ConfigurationManager.AppSettings["ESMIModelChangeCompleteAddress"] ?? string.Empty;
                                string writeDeviceError = ConfigurationManager.AppSettings["ErrorAddress"] ?? string.Empty;
                                string writeDeviceDetectReady = ConfigurationManager.AppSettings["DetectReadyAddress"] ?? string.Empty;
                                string writeDeviceAlive = ConfigurationManager.AppSettings["AliveAddress"] ?? string.Empty;
                                string writeDeviceLearnMode = ConfigurationManager.AppSettings["LearnModeAddress"] ?? string.Empty;

                                writeDevice = JsonConvert.DeserializeObject<PlcVariable>(jsonText);
                                if (writeDevice.ChannelNo > 1)
                                {
                                    writeDeviceOn = ConfigurationManager.AppSettings[$"AnomalyOnAddress{writeDevice.ChannelNo}"] ?? string.Empty;
                                    writeDeviceOff = ConfigurationManager.AppSettings[$"AnomalyOffAddress{writeDevice.ChannelNo}"] ?? string.Empty;
                                }

                                if (writeDevice.VarName == "AnomalyOnAddress")
                                {
                                    if (writeDevice.ReadValue == 1)
                                    {
                                        if (UseLGDTMGlassScenario == true)
                                        {
                                            results[writeDevice.ChannelNo - 1] = 2;

                                            int[] result_count = new int[3];
                                            for (int i = 0; i < GlobalInfo.NumChannel; i++)
                                            {
                                                result_count[results[i]]++;
                                            }
                                            if (result_count[2] == 1)
                                                plc.SetAPLCValueOn(writeDeviceOn);

                                            if (result_count[0] == 0)
                                                results.Initialize();

                                            if (AnomalyAutoOff == true)
                                            {
                                                Thread.Sleep(1000);
                                                plc.SetAPLCValueOff(writeDeviceOn);
                                            }
                                        }
                                        else
                                        {
                                            if (UseBusy == true)
                                                plc.SetAPLCValueOff(writeDeviceBusy);

                                            plc.SetAPLCValueOn(writeDeviceOn);
                                            plc.SetAPLCValueOff(writeDeviceOff);
                                            plc.SetAPLCValueOff(writeDeviceCapture);

                                            if (AnomalyAutoOff == true)
                                            {
                                                Thread.Sleep(1000);
                                                plc.SetAPLCValueOff(writeDeviceOn);
                                            }
                                        }
                                    }
                                    else if (writeDevice.ReadValue == 0)
                                    {
                                        plc.SetAPLCValueOff(writeDeviceOn);
                                    }
                                }
                                else if (writeDevice.VarName == "AnomalyOffAddress")
                                {
                                    if (writeDevice.ReadValue == 1)
                                    {
                                        if (UseLGDTMGlassScenario == true)
                                        {
                                            results[writeDevice.ChannelNo - 1] = 1;

                                            int[] result_count = new int[3];
                                            for (int i = 0; i < GlobalInfo.NumChannel; i++)
                                            {
                                                result_count[results[i]]++;
                                            }
                                            if (result_count[1] == GlobalInfo.NumChannel)
                                                plc.SetAPLCValueOn(writeDeviceOff);

                                            if (result_count[0] == 0)
                                                results.Initialize();

                                            if (AnomalyAutoOff == true)
                                            {
                                                Thread.Sleep(1000);
                                                plc.SetAPLCValueOff(writeDeviceOff);
                                            }
                                        }
                                        else
                                        {
                                            if (UseBusy == true)
                                                plc.SetAPLCValueOff(writeDeviceBusy);
                                            plc.SetAPLCValueOff(writeDeviceOn);
                                            plc.SetAPLCValueOn(writeDeviceOff);
                                            plc.SetAPLCValueOff(writeDeviceCapture);
                                            if (AnomalyAutoOff == true)
                                            {
                                                Thread.Sleep(500);
                                                plc.SetAPLCValueOff(writeDeviceOff);
                                            }
                                        }
                                    }
                                    else if (writeDevice.ReadValue == 0)
                                    {
                                        plc.SetAPLCValueOff(writeDeviceOff);
                                    }
                                }
                                else if (writeDevice.VarName == "NotDetecting")
                                {
                                    if (UseLGDTMGlassScenario == true)
                                    {
                                        results[writeDevice.ChannelNo - 1] = 1;

                                        int[] result_count = new int[3];
                                        for (int i = 0; i < GlobalInfo.NumChannel; i++)
                                        {
                                            result_count[results[i]]++;
                                        }
                                        if (result_count[1] == GlobalInfo.NumChannel)
                                            plc.SetAPLCValueOn(writeDeviceOff);

                                        if (result_count[0] == 0)
                                            results.Initialize();
                                    }
                                }
                                else if (writeDevice.VarName == "AlarmOff")
                                {
                                    plc.SetAPLCValueOff(writeDeviceOn);
                                    plc.SetAPLCValueOff(writeDeviceOff);
                                }
                                else if (writeDevice.VarName == "CaptureCompleteAddress" && writeDevice.ReadValue == 1)
                                {
                                    plc.SetAPLCValueOn(writeDeviceCapture);
                                }
                                else if (writeDevice.VarName == "BusyAddress")
                                {
                                    if (writeDevice.ReadValue == 1)
                                        plc.SetAPLCValueOn(writeDeviceBusy);
                                    else if (writeDevice.ReadValue == 0)
                                        plc.SetAPLCValueOff(writeDeviceBusy);
                                }
                                else if (writeDevice.VarName == "ESMIModelChangeCompleteAddress")
                                {
                                    if (writeDevice.ReadValue == 1)
                                        plc.SetAPLCValueOn(writeDeviceModelChanged);
                                    else if (writeDevice.ReadValue == 0)
                                        plc.SetAPLCValueOff(writeDeviceModelChanged);
                                }
                                else if (writeDevice.VarName == "ErrorAddress")
                                {
                                    if (writeDevice.ReadValue == 1)
                                        plc.SetAPLCValueOn(writeDeviceError);
                                    else if (writeDevice.ReadValue == 0)
                                        plc.SetAPLCValueOff(writeDeviceError);
                                }
                                else if (writeDevice.VarName == "DetectReadyAddress")
                                {
                                    if (writeDevice.ReadValue == 1)
                                        plc.SetAPLCValueOn(writeDeviceDetectReady);
                                    else if (writeDevice.ReadValue == 0)
                                        plc.SetAPLCValueOff(writeDeviceDetectReady);
                                }
                                else if (writeDevice.VarName == "AliveAddress")
                                {
                                    if (writeDevice.ReadValue == 1)
                                        plc.SetAPLCValueOn(writeDeviceAlive);
                                    else if (writeDevice.ReadValue == 0)
                                        plc.SetAPLCValueOff(writeDeviceAlive);
                                }
                                else if (writeDevice.VarName == "LearnModeAddress")
                                {
                                    if (writeDevice.ReadValue == 1)
                                        plc.SetAPLCValueOn(writeDeviceLearnMode);
                                    else if (writeDevice.ReadValue == 0)
                                        plc.SetAPLCValueOff(writeDeviceLearnMode);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"{ex.Message}\r\n{ex.StackTrace}");
                        }
                    }
                    Response(context, msgTitle);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"{ex.Message}\r\n{ex.StackTrace}");
            }
        }

        public static object DeserializeFromStream(StreamReader sr)
        {
            var serializer = new JsonSerializer();

            using (var jsonTextReader = new JsonTextReader(sr))
            {
                return serializer.Deserialize(jsonTextReader);
                //return jsonTextReader;
                //var json1 = serializer.Deserialize(jsonTextReader);
            }
        }

        /// <summary>
        /// PieIF에게 회신한다
        /// </summary>
        /// <param name="context"></param>
        private void Response(HttpListenerContext context, string msgTitle)
        {
            HttpListenerResponse response = context.Response;
            string responseString;

            responseString = msgTitle;

            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);

            // Get a response stream and write the response to it.
            response.ContentLength64 = buffer.Length;
            System.IO.Stream output = response.OutputStream;
            output.Write(buffer, 0, buffer.Length);

            // You must close the output stream.
            output.Close();
        }

        /// <summary>
        /// 소켓으로 웹서버를 오픈한 경우 메시지 받는 함수
        /// </summary>
        /// <param name="client"></param>
        /// <returns></returns>
        public String Recieve(Socket client)
        {
            String _data_str = "";
            byte[] _data = new byte[4096];
            client.Receive(_data);

            String[] _buf = Encoding.Default.GetString(_data).Split("\r\n".ToCharArray());
            //MessageBox.Show(_buf.Length + ">>> " + _buf[0]);

            JObject jObj = JObject.Parse(_data.ToString());

            if (_buf[0].IndexOf("GET") != -1)
            {
                _data_str = _buf[0].Replace("GET ", "").Replace("HTTP/1.1", "").Trim();
            }
            else
            {
                _data_str = _buf[0].Replace("POST ", "").Replace("HTTP/1.1", "").Trim();
            }
            if (_data_str.Trim() == "/")
            {
                _data_str += "index.html";
            }
            int pos = _data_str.IndexOf("?");
            if (pos > 0)
            {
                _data_str = _data_str.Remove(pos);
            }
            return "web" + _data_str;
        }

        //public async Task AlarmOccurExe(ScenarioMode type = ScenarioMode.Always)
        //{
        //    string EquipmentId = ConfigurationManager.AppSettings["AppFolder"];

        //    ALARMSUBITEMLIST alarmItem;
        //    if (type == ScenarioMode.Always)
        //    {
        //        alarmItem = new ALARMSUBITEMLIST("CODE002", "DETECT_HUMAN", "0", "Human is detected.");
        //    }
        //    else
        //    {
        //        alarmItem = new ALARMSUBITEMLIST("CODE001", "DETECT_ADPART", "0", "Bad part is inserted.");
        //    }

        //    List<ALARMSUBITEMLIST> alarmItems = new List<ALARMSUBITEMLIST>
        //    {
        //        alarmItem
        //    };
        //    C_EASR easr = new C_EASR();
        //    easr.EASR = new AlarmInfo(EquipmentId, alarmItems);
        //    //string json = JsonConvert.SerializeObject(new AlarmOccur(EquipmentId, alarmItems));
        //    string json = JsonConvert.SerializeObject(easr);

        //    SendHttpMessage("EASR", json);

        //    await Task.Delay(10);
        //}

        //public async Task AlarmReleaseExe(ScenarioMode type = ScenarioMode.Always)
        //{
        //    string EquipmentId = ConfigurationManager.AppSettings["AppFolder"];

        //    ALARMSUBITEMLIST alarmItem;
        //    if (type == ScenarioMode.Always)
        //    {
        //        alarmItem = new ALARMSUBITEMLIST("CODE002", "DETECT_HUMAN", "0", "Human detection alarm is released.");
        //    }
        //    else
        //    {
        //        alarmItem = new ALARMSUBITEMLIST("CODE001", "DETECT_ADPART", "0", "Bad part alarm is released.");
        //    }

        //    List<ALARMSUBITEMLIST> alarmItems = new List<ALARMSUBITEMLIST>
        //    {
        //        alarmItem
        //    };
        //    C_EARR earr = new C_EARR();
        //    earr.EARR = new AlarmInfo(EquipmentId, alarmItems);
        //    //string json = JsonConvert.SerializeObject(new AlarmOccur(EquipmentId, alarmItems));
        //    string json = JsonConvert.SerializeObject(earr);

        //    for (int i = 0; i < 5; i++)
        //    {
        //        SendHttpMessage("EARR", json);
        //        Thread.Sleep(200);
        //    }

        //    await Task.Delay(10);
        //}        
    }
}
