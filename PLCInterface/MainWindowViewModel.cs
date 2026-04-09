using System;
using System.Configuration;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Input;
using System.Collections.ObjectModel;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Controls;
using System.Windows;
using System.IO.Ports;

namespace PLCInterface
{
    class MainWindowViewModel : ViewModelBase
    {
        private static string ProgramVersion { get; set; } = "0.8.8"; // 삼천산업 MES

        [DllImport("kernel32")]
        public static extern Int32 GetCurrentProcessId();

        //MelsecInterface melsec;
        CommonInterface plc;
        private System.Timers.Timer PlcInterfaceTimer = null;
        private System.Timers.Timer PlcAliveTimer = null;
        private System.Timers.Timer ConnectResetTimer = null;
        private System.Timers.Timer ReadBarcodeTimer = null;
        private System.Timers.Timer ConnectRefreshTimer = null;

        HoneywellBarcodeInterface Barcode_Interface;

        private string plcStatusColor = string.Empty;
        public string PlcStatusColor
        {
            get
            {
                return plcStatusColor;
            }
            set
            {
                plcStatusColor = value;
                OnPropertyChanged("PlcStatusColor");
            }
        }

        private ObservableCollection<string> uiWebStatusColor;
        public ObservableCollection<string> UiWebStatusColor
        {
            get
            {
                return uiWebStatusColor;
            }
            set
            {
                uiWebStatusColor = value;
                OnPropertyChanged("UiWebStatusColor");
            }
        }

        private string startPressedColor = string.Empty;
        public string StartPressedColor
        {
            get
            {
                return startPressedColor;
            }
            set
            {
                startPressedColor = value;
                OnPropertyChanged("StartPressedColor");
            }
        }

        private string stopPressedColor = string.Empty;
        public string StopPressedColor
        {
            get
            {
                return stopPressedColor;
            }
            set
            {
                stopPressedColor = value;
                OnPropertyChanged("StopPressedColor");
            }
        }

        private string infoMessage = string.Empty;
        public string InfoMessage
        {
            get
            {
                return infoMessage;
            }
            set
            {
                infoMessage = value;
                OnPropertyChanged("InfoMessage");
            }
        }

        private ICommand startInterfaceCommand;
        public ICommand StartInterfaceCommand
        {
            get
            {
                return startInterfaceCommand;
            }
            set
            {
                startInterfaceCommand = value;
            }
        }

        private ICommand stopInterfaceCommand;
        public ICommand StopInterfaceCommand
        {
            get
            {
                return stopInterfaceCommand;
            }
            set
            {
                stopInterfaceCommand = value;
            }
        }
        
        private ICommand usbTesterButtonCommand;
        public ICommand UsbTesterButtonCommand
        {
            get
            {
                return usbTesterButtonCommand;
            }
            set
            {
                usbTesterButtonCommand = value;
            }
        }
        private ICommand manualTriggerButtonCommand;
        public ICommand ManualTriggerButtonCommand
        {
            get
            {
                return manualTriggerButtonCommand;
            }
            set
            {
                manualTriggerButtonCommand = value;
            }
        }

        // SubTitle
        private string mainTitle = string.Empty;
        public string MainTitle
        {
            get
            {
                return mainTitle;
            }
            set
            {
                mainTitle = value;
                OnPropertyChanged("MainTitle");
            }
        }

        private Boolean aliveOn = false;
        public Boolean AliveOn
        {
            get
            {
                return aliveOn;
            }
            set
            {
                aliveOn = value;
            }
        }
        private string aliveDevice = string.Empty;
        public string AliveDevice
        {
            get
            {
                return aliveDevice;
            }
            set
            {
                aliveDevice = value;
            }
        }

        private Visibility usbTesterButtonVisible;
        public Visibility UsbTesterButtonVisible
        {
            get
            {
                return usbTesterButtonVisible;
            }
            set
            {
                usbTesterButtonVisible = value;
                OnPropertyChanged("UsbTesterButtonVisible");
            }
        }

        public MainWindowViewModel()
        {
            try
            {
                Logger.Info($"★★★ Anomaly Detection PLC Communicator Version: [{ProgramVersion}] ★★★");

                // 멀티프로그램 허용하지 않았는데, 동명의 다른 프로세스가 살아있다면 죽이고 시작함
                //if (!GlobalInfo.MultiProgram)
                {
                    if (IsProgramRunning())
                    {
                        KillProgram(true);
                    }
                }

                // Initialization Global Variable
                InitialzeGloblaVariables();

                byte plcType = Convert.ToByte(ConfigurationManager.AppSettings["PlcType"] ?? "1");
                if (plcType == 1)
                    plc = new MelsecInterface();
                else if (plcType == 2)
                    plc = new ModbusInterface();
                else if (plcType == 3)
                    plc = new AdvantechDAQInterface();
                //else if (plcType == 4) // Siemens
                //    plc = new AdvantechDAQInterface();
                else if (plcType == 5)
                    plc = new LSElectricInterface();
                else if (plcType == 6)
                    plc = new ADLinkDIOInterface(); // LGD에 3으로 배포되었으나 4로 변경 --> 6으로 변경
                else if (plcType == 7)
                    plc = new FastechEziIoInterface();
                else
                    plc = new MelsecInterface();

                StartInterfaceCommand = new RelayCommand(StartInterfaceCommandExe, param => this.CanExecute);
                StopInterfaceCommand = new RelayCommand(StopInterfaceCommandExe, param => this.CanExecute);
                UsbTesterButtonCommand = new RelayCommand(UsbTesterButtonCommandExe, param => this.CanExecute);
                ManualTriggerButtonCommand = new RelayCommand(ManualTriggerButtonCommandExe, param => this.CanExecute);

                UiWebStatusColor = new ObservableCollection<string>();

                PlcStatusColor = "gray";
                for (int i = 0; i < GlobalInfo.NumChannel; i++)
                    UiWebStatusColor.Add("gray");
                StartPressedColor = "gray";
                StopPressedColor = "gray";
                InfoMessage = $"Configuration Setting Success: {plc.IsConfigurationSuccess}";

                string subTitle = ConfigurationManager.AppSettings["SubTitle"] ?? string.Empty; // SubTitle
                MainTitle = $"Ver.{ProgramVersion} {subTitle}";                                 // SubTitle

                PlcInterfaceTimer = new System.Timers.Timer
                {
                    Interval = 100,
                    AutoReset = true,
                    Enabled = false
                };
                PlcInterfaceTimer.Elapsed += new ElapsedEventHandler(ReadPlcValue);

                PlcAliveTimer = new System.Timers.Timer
                {
                    Interval = 2000,
                    AutoReset = true,
                    Enabled = false
                };

                if (((ConfigurationManager.AppSettings["UseAlive"] ?? string.Empty).ToUpper().Equals("TRUE"))
                    && ((ConfigurationManager.AppSettings["UseUIAlive"] ?? "FALSE").ToUpper().Equals("FALSE")))
                {
                    PlcAliveTimer.Elapsed += new ElapsedEventHandler(WriteAlive);
                    AliveDevice = ConfigurationManager.AppSettings["AliveAddress"] ?? string.Empty;
                }
                else
                    AliveDevice = string.Empty;

                // HoneywellBarcodeReader 추가
                ReadBarcodeTimer = new System.Timers.Timer
                {
                    Interval = 200,
                    AutoReset = true,
                    Enabled = false
                };
                if ((ConfigurationManager.AppSettings["UseHoneywellBarcodeReader"] ?? "FALSE").ToUpper().Equals("TRUE"))
                {
                    Logger.Info($"Use Honeywell Barcode Reader mode");
                    Barcode_Interface = new HoneywellBarcodeInterface();
                    ReadBarcodeTimer.Elapsed += new ElapsedEventHandler(ReadBarcode);
                }

                if ((ConfigurationManager.AppSettings["UsePeriodicConnectReset"] ?? "FALSE").ToUpper().Equals("TRUE"))
                {
                    ConnectResetTimer = new System.Timers.Timer
                    {
                        Interval = 60000,  // 1분
                        AutoReset = true,
                        Enabled = true
                    };
                    ConnectResetTimer.Elapsed += new ElapsedEventHandler(ConnectResetTimerHandler);
                }
                ConnectRefreshTimer = new System.Timers.Timer
                {
                    Interval = 2000,
                    AutoReset = true,
                    Enabled = true
                };
                ConnectRefreshTimer.Elapsed += new ElapsedEventHandler(ConnectRefreshTimerHandler);

                // 0.7.2 : USB 경광등 테스트
                if (GlobalInfo.UseAuroraTowerLamp)
                    UsbTesterButtonVisible = Visibility.Visible;
                else
                    UsbTesterButtonVisible = Visibility.Collapsed;

                var httpServer = new HttpServer();
                Task.Run(() => httpServer.ActivateHttpService());

                if ((ConfigurationManager.AppSettings["AutoRun"] ?? string.Empty).ToUpper().Equals("TRUE"))
                {
                    try
                    {
                        Thread.Sleep(3000);
                        InfoMessage = plc.StartInterface();
                        SetConnectionStatus();
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Exception on {System.Reflection.MethodBase.GetCurrentMethod().Name} >>> {ex.Message}\r\n{ex.StackTrace}");
                    }
                    finally { }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Exception on {System.Reflection.MethodBase.GetCurrentMethod().Name} >>> {ex.Message}\r\n{ex.StackTrace}");
            }
            finally { }            
        }

        private void InitialzeGloblaVariables()
        {
            try
            {
                GlobalInfo.InitializeGlobalInfo();
            }
            catch (Exception ex)
            {
                Logger.Error($"Exception on {System.Reflection.MethodBase.GetCurrentMethod().Name} >>> {ex.Message}\r\n{ex.StackTrace}");
            }
            finally { }
        }

        private void StartInterfaceCommandExe(object obj)
        {
            try
            {
                if (GlobalInfo.SkipPLCInterface == false)
                    InfoMessage = plc.StartInterface();
                SetConnectionStatus();
            }
            catch(Exception ex)
            {
                Logger.Error($"Exception on {System.Reflection.MethodBase.GetCurrentMethod().Name} >>> {ex.Message}\r\n{ex.StackTrace}");
            }
            finally { }
        }

        private void StopInterfaceCommandExe(object obj)
        {
            try
            {
                if (GlobalInfo.SkipPLCInterface == false)
                {
                    if (plc.IsAlive) // 0.5.5
                    {
                        // true가 되기까지 기다렸다가, false로 세팅
                        int loopCount = 0;
                        while (!PlcInterfaceTimer.Enabled)
                        {
                            Logger.Info("Stop Retry");
                            Thread.Sleep(100);
                            if (loopCount > 5)
                                break;
                            loopCount++;
                        }
                    }

                    PlcInterfaceTimer.Enabled = false;
                    PlcAliveTimer.Enabled = false;
                	if ((ConfigurationManager.AppSettings["UseHoneywellBarcodeReader"] ?? "FALSE").ToUpper().Equals("TRUE"))
                    	ReadBarcodeTimer.Enabled = false;
                    WriteAliveOff();
                }

                StartPressedColor = "gray";
                StopPressedColor = "greenyellow";
                PlcStatusColor = "yellow";
                for (int i = 0; i < GlobalInfo.NumChannel; i++)
                    UiWebStatusColor[i] = "yellow";
            }
            catch (Exception ex)
            {
                Logger.Error($"Exception on {System.Reflection.MethodBase.GetCurrentMethod().Name} >>> {ex.Message}\r\n{ex.StackTrace}");
            }
            finally { }
        }
        
        private void UsbTesterButtonCommandExe(object obj)
        {
            try
            {
                if (flash2 == 0)
                    flash2 = 1;
                else
                    flash2 = 0;
                write_tower_status();
            }
            catch (Exception ex)
            {
                Logger.Error($"Exception on {System.Reflection.MethodBase.GetCurrentMethod().Name} >>> {ex.Message}\r\n{ex.StackTrace}");
            }
            finally { }
        }

        int ValueOn = 0;
        private void ManualTriggerButtonCommandExe(object obj)
        {
            try
            {
                plc.SendManualTrigger("TriggerAddress", ValueOn);
                ValueOn = (ValueOn == 0) ? 1 : 0;
            }
            catch (Exception ex)
            {
                Logger.Error($"Exception on {System.Reflection.MethodBase.GetCurrentMethod().Name} >>> {ex.Message}\r\n{ex.StackTrace}");
            }
            finally { }
        }

        int led0, led1, led2, led3, led4, flash0, flash1, flash2, flash3, flash4, sound_select;
        private bool write_tower_status()
        {
            try
            {
                AuroraUSBTowerLampInterface.aurora_set_tower_switch(led0, led1, led2, led3, led4, flash0, flash1, flash2, flash3, flash4, sound_select);
            }
            catch { }
            return true;
        }


        private void SetConnectionStatus()
        {
            StartPressedColor = "greenyellow";
            StopPressedColor = "gray";

            if ((ConfigurationManager.AppSettings["UseHoneywellBarcodeReader"] ?? "FALSE").ToUpper().Equals("TRUE"))
            {
                // enable Honeywell Barcode Reader timer
                ReadBarcodeTimer.Enabled = true;
                Logger.Info($"ReadBarcodeTimer is enabled.");
            }

            if (plc.IsAlive)
            {
                PlcStatusColor = "greenyellow";
                //StartPressedColor = "greenyellow";
                //StopPressedColor = "gray";
                if (plc.GetReadDevicesCount() > 0)
                    PlcInterfaceTimer.Enabled = true;
                else
                {
                    for (int i=0; i< GlobalInfo.NumChannel; i++)
                        UiWebStatusColor[i] = "greenyellow";
                }
                if (((ConfigurationManager.AppSettings["UseAlive"] ?? string.Empty).ToUpper().Equals("TRUE"))
                   && ((ConfigurationManager.AppSettings["UseUIAlive"] ?? "FALSE").ToUpper().Equals("FALSE")))
                    PlcAliveTimer.Enabled = true;
            }
            else
            {
                PlcStatusColor = "red";
                //StartPressedColor = "gray";
                //StopPressedColor = "gray";
            }
        }

        private void ReadPlcValue(object source, ElapsedEventArgs e)
        {
            try
            {
                PlcInterfaceTimer.Enabled = false;
                InfoMessage = plc.ReadPlcValues();

                for (int i = 0; i < GlobalInfo.NumChannel; i++)
                {
                    if (GlobalInfo.IsSuccessToSend[i])
                        UiWebStatusColor[i] = "greenyellow";
                    else
                        UiWebStatusColor[i] = "red";
                }
                
                PlcInterfaceTimer.Interval = Convert.ToInt32(ConfigurationManager.AppSettings["PlcReadInterval"] ?? "100"); // 0.5.2
            }
            catch (Exception ex)
            {
                Logger.Error($"Exception on {System.Reflection.MethodBase.GetCurrentMethod().Name} >>> {ex.Message}\r\n{ex.StackTrace}");
                InfoMessage = "Error while reading & operating PLC values";
            }
            finally
            {
                PlcInterfaceTimer.Enabled = true;
            }
        }

        private void WriteAlive(object source, ElapsedEventArgs e)
        {
            try
            {
                PlcAliveTimer.Enabled = false;
                if (AliveDevice != string.Empty)
                {
                    int res;
                    if ((ConfigurationManager.AppSettings["AliveToggle"] ?? string.Empty).ToUpper().Equals("TRUE"))
                    {
                        if (AliveOn == true)
                        {
                            res = plc.SetAPLCValueOff(AliveDevice);
                            AliveOn = false;
                        }
                        else
                        {
                            res = plc.SetAPLCValueOn(AliveDevice);
                            AliveOn = true;
                        }
                    }
                    else
                        res = plc.SetAPLCValueOn(AliveDevice);
                }
                
                PlcAliveTimer.Interval = Convert.ToInt32(ConfigurationManager.AppSettings["PlcAliveInterval"] ?? "2000"); // 0.5.2
            }
            catch (Exception ex)
            {
                Logger.Error($"Exception on {System.Reflection.MethodBase.GetCurrentMethod().Name} >>> {ex.Message}\r\n{ex.StackTrace}");
                InfoMessage = "Error while reading & operating PLC values";
            }
            finally
            {
                PlcAliveTimer.Enabled = true;
            }
        }

        private void WriteAliveOff()
        {
            try
            {
                if (AliveDevice != string.Empty)
                {
                    int res = plc.SetAPLCValueOff(AliveDevice);
                    AliveOn = false;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Exception on {System.Reflection.MethodBase.GetCurrentMethod().Name} >>> {ex.Message}\r\n{ex.StackTrace}");
                InfoMessage = "Error while reading & operating PLC values";
            }
        }

        private void ConnectResetTimerHandler(object source, ElapsedEventArgs e)
        {
            try
            {
                DateTime nowTime = DateTime.Now;
                int checkHour = Convert.ToInt32(ConfigurationManager.AppSettings["ConnectResetHour"] ?? "7");
                int checkMinute = Convert.ToInt32(ConfigurationManager.AppSettings["ConnectResetMinute"] ?? "0");

                if (nowTime.Hour == checkHour && nowTime.Minute == checkMinute)
                {
                    Logger.Info($"Periodic Connect Reset : {nowTime.Hour}");
                    // StopInterfaceCommandExe
                    if (plc.IsAlive)
                    {
                        // true가 되기까지 기다렸다가, false로 세팅
                        int loopCount = 0;
                        while (!PlcInterfaceTimer.Enabled)
                        {
                            Logger.Info("Stop Retry");
                            Thread.Sleep(100);
                            if (loopCount > 5)
                                break;
                            loopCount++;
                        }
                    }
                    PlcInterfaceTimer.Enabled = false;
                    PlcAliveTimer.Enabled = false;
                    if ((ConfigurationManager.AppSettings["UseHoneywellBarcodeReader"] ?? "FALSE").ToUpper().Equals("TRUE"))
                        ReadBarcodeTimer.Enabled = false;

                    Thread.Sleep(100);

                    // StartInterfaceCommandExe
                    InfoMessage = plc.StartInterface();
                    SetConnectionStatus();
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Exception on {System.Reflection.MethodBase.GetCurrentMethod().Name} >>> {ex.Message}\r\n{ex.StackTrace}");
            }
        }

        private void ConnectRefreshTimerHandler(object source, ElapsedEventArgs e)
        {
            try
            {
                for (int i = 0; i < GlobalInfo.NumChannel; i++)
                {
                    Logger.Debug($"ConnectRefreshTimerHandler {i}");
                    if (GlobalInfo.IsSuccessToSend[i])
                    {
                        UiWebStatusColor[i] = "greenyellow";
                    }
                    else
                    {
                        UiWebStatusColor[i] = "red";
                    }

                    if (GlobalInfo.IsConnectedToMES)
                    {
                        PlcStatusColor = "greenyellow";
                        GlobalInfo.IsConnectedToMES = false;
                    }
                    else
                    {
                        PlcStatusColor = "red";
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Exception on {System.Reflection.MethodBase.GetCurrentMethod().Name} >>> {ex.Message}\r\n{ex.StackTrace}");
            }
        }

        private bool IsProgramRunning()
        {
            bool isRunning = false;
            try
            {
                string currentProcessName = Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetExecutingAssembly().Location);

                //현재 프로세스 목록 가져오기
                Process[] pslist = Process.GetProcesses();

                string list = string.Empty;
                int processCount = 0;
                for (int i = 0; i < pslist.Length; i++)
                {
                    list = pslist[i].ProcessName;
                    if (list.Equals(currentProcessName))
                    {
                        processCount++;
                    }
                }
                if (processCount > 1)
                {
                    Logger.Info($"{currentProcessName} is already running");
                    isRunning = true;
                }
                return isRunning;
            }
            catch (Exception ex)
            {
                Logger.Error($"Exception on {System.Reflection.MethodBase.GetCurrentMethod().Name} >>> {ex.Message}\r\n{ex.StackTrace}");
                return true;
            }
            finally { }
        }

        private void KillProgram(bool isStart = false)
        {
            string currentProcessName = Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetExecutingAssembly().Location);

            Process[] array = Process.GetProcessesByName(currentProcessName);
            int thisProcessId = GetCurrentProcessId();
            Process thisProcess = Process.GetProcessById(thisProcessId);

            // 나 빼고 다 죽임
            if (isStart)
            {
                if (array.Length > 0)
                {
                    for (int iProcess = 0; iProcess < array.Length; iProcess++)
                    {
                        try
                        {
                            if (array[iProcess].Id != thisProcessId)
                            {
                                Logger.Debug($"♠ PID:[{array[iProcess].Id}], {array[iProcess].ProcessName} is Dying. I am [{thisProcessId}] ♠");
                                array[iProcess].Kill();
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"[{array.Length}] Exception on {System.Reflection.MethodBase.GetCurrentMethod().Name} >>> {ex.Message}\r\n{ex.StackTrace}");
                        }
                    }
                }
            }
            // 나를 죽임
            else
            {
                //Utility.SaveCurrentStatus(ApplicationStatus.OnDetect);
                Logger.Info($"♥ Killing me softly with his song ♥");
                thisProcess.Kill();
            }
        }

        private void ReadBarcode(object source, ElapsedEventArgs e)
        {
            try
            {
                ReadBarcodeTimer.Enabled = false;
                Barcode_Interface.ReadBarcode();
            }
            catch (Exception ex)
            {
                Logger.Error($"Exception on {System.Reflection.MethodBase.GetCurrentMethod().Name} >>> {ex.Message}\r\n{ex.StackTrace}");
            }
            finally 
            {
                ReadBarcodeTimer.Enabled = true;
            }
        }
    }    
}
