using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Input;

namespace PLCInterface
{
    abstract class CommonInterface
    {
        public bool IsConfigurationSuccess { get; set; } = false;
        public bool IsAlive { get; set; } = false;

        public abstract string StartInteface();
        public abstract int OpenConnection();
        public abstract int CloseConnection();
        public abstract string ReadPlcValues();
        public abstract int SetAPLCValueOn(string device);
        public abstract int SetAPLCValueOff(string device);
    }

    class MainWindowViewModel : ViewModelBase
    {
        private static string ProgramVersion { get; set; } = "0.5.11";   // 2022.05.28 Alive bit 추가

        //MelsecInterface melsec;
        CommonInterface plc;
        private System.Timers.Timer PlcInterfaceTimer = null;
        private System.Timers.Timer PlcAliveTimer = null;

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

        private string uiWebStatusColor = string.Empty;
        public string UiWebStatusColor
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

        public MainWindowViewModel()
        {
            Logger.Info($"★★★ Anomaly Detection PLC Communicator Version: [{ProgramVersion}] ★★★");

            byte plcType = Convert.ToByte(ConfigurationManager.AppSettings["PlcType"] ?? "1");
            if (plcType == 1)
                plc = new MelsecInterface();
            else if(plcType == 2)
                plc = new ModbusInterface();
            else
                plc = new MelsecInterface();

            StartInterfaceCommand = new RelayCommand(StartInterfaceCommandExe, param => this.CanExecute);
            StopInterfaceCommand = new RelayCommand(StopInterfaceCommandExe, param => this.CanExecute);

            PlcStatusColor = "gray";
            UiWebStatusColor = "gray";
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
            PlcAliveTimer.Elapsed += new ElapsedEventHandler(WriteAlive);

            AliveDevice = ConfigurationManager.AppSettings["AliveAddress"] ?? string.Empty;

            var httpServer = new HttpServer();
            Task.Run(() => httpServer.ActivateHttpService());
        }

        private void StartInterfaceCommandExe(object obj)
        {
            try
            {
                InfoMessage = plc.StartInteface();
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

                StartPressedColor = "gray";
                StopPressedColor = "greenyellow";
                PlcStatusColor = "yellow";
                UiWebStatusColor = "yellow";
            }
            catch (Exception ex)
            {
                Logger.Error($"Exception on {System.Reflection.MethodBase.GetCurrentMethod().Name} >>> {ex.Message}\r\n{ex.StackTrace}");
            }
            finally { }
        }

        private void SetConnectionStatus()
        {
            StartPressedColor = "greenyellow";
            StopPressedColor = "gray";

            if (plc.IsAlive)
            {
                PlcStatusColor = "greenyellow";
                //StartPressedColor = "greenyellow";
                //StopPressedColor = "gray";
                PlcInterfaceTimer.Enabled = true;
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
                if (HttpMessage.IsSuccessToSend)
                {
                    UiWebStatusColor = "greenyellow";
                }
                else
                {
                    UiWebStatusColor = "red";
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
    }
}
