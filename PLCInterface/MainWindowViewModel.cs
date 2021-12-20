using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Input;

namespace PLCInterface
{
    class MainWindowViewModel : ViewModelBase
    {
        private static string ProgramVersion { get; set; } = "0.5.0";   // 2021.12.16

        MelsecInterface melsec;
        private System.Timers.Timer PlcInterfaceTimer = null;

        private string statusColor = string.Empty;
        public string StatusColor
        {
            get
            {
                return statusColor;
            }
            set
            {
                statusColor = value;
                OnPropertyChanged("StatusColor");
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

        public MainWindowViewModel()
        {
            Logger.Info($"Anomaly Detection PLC Communicator Version: [{ProgramVersion}]");

            melsec = new MelsecInterface();
            StartInterfaceCommand = new RelayCommand(StartInterfaceCommandExe, param => this.CanExecute);
            StopInterfaceCommand = new RelayCommand(StopInterfaceCommandExe, param => this.CanExecute);

            StatusColor = "gray";
            StartPressedColor = "gray";
            StopPressedColor = "gray";
            InfoMessage = $"Configuration Setting Success: {melsec.IsConfigurationSuccess}";

            PlcInterfaceTimer = new System.Timers.Timer
            {
                Interval = 250,
                AutoReset = true,
                Enabled = false
            };
            PlcInterfaceTimer.Elapsed += new ElapsedEventHandler(ReadPlcValue);
        }

        private void StartInterfaceCommandExe(object obj)
        {
            try
            {
                InfoMessage = melsec.StartInteface();
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
                // true가 되기까지 기다렸다가, false로 세팅
                while (!PlcInterfaceTimer.Enabled)
                {
                    Thread.Sleep(100);
                }

                PlcInterfaceTimer.Enabled = false;

                StartPressedColor = "gray";
                StopPressedColor = "greenyellow";                
                StatusColor = "yellow";
            }
            catch (Exception ex)
            {
                Logger.Error($"Exception on {System.Reflection.MethodBase.GetCurrentMethod().Name} >>> {ex.Message}\r\n{ex.StackTrace}");
            }
            finally { }
        }

        private void SetConnectionStatus()
        {
            if (melsec.IsAlive)
            {
                StatusColor = "lightgreen";
                StartPressedColor = "greenyellow";
                StopPressedColor = "gray";
                PlcInterfaceTimer.Enabled = true;
            }
            else
            {
                StatusColor = "red";
                StartPressedColor = "gray";
                StopPressedColor = "gray";
            }
        }
        private void ReadPlcValue(object source, ElapsedEventArgs e)
        {
            try
            {
                PlcInterfaceTimer.Enabled = false;
                InfoMessage = melsec.ReadPlcValues();
            }
            catch (Exception ex)
            {
                Logger.Error($"Exception on {System.Reflection.MethodBase.GetCurrentMethod().Name} >>> {ex.Message}\r\n{ex.StackTrace}");
                InfoMessage = "Error while reading Plc Values";
            }
            finally
            {
                PlcInterfaceTimer.Enabled = true;
            }
        }
    }
}
