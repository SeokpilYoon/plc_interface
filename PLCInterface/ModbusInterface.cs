using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using System.Windows;

using Modbus.Device;
using System.IO.Ports;
using System.Net.Sockets;

namespace PLCInterface
{
    class ModbusInterface : CommonInterface
    {
        public TcpClient tcpclient;
        public ModbusIpMaster master;
        public string address { get; set; }
        public int port { get; set; }
        public byte slaveID { get; set; }
        private static int RetryNumLimit { get; set; } = 3;
        public string DeviceRandomToRead { get; set; } = string.Empty;
        public bool IsUIReady { get; set; } = false;

        int[] readValues;

        public string DeviceRandomToWrite { get; set; } = string.Empty;

        List<PlcVariable> ReadDevices;
        List<PlcVariable> WriteDevices;
        //private Task plcInterfaceTask;
        private string PreviousRead { get; set; } = string.Empty;

        public ModbusInterface()
        {
            try
            {
                #region Read Configuration

                int interfaceIndex = 0;
                ReadDevices = new List<PlcVariable>();

                int modelQty = Convert.ToInt32(ConfigurationManager.AppSettings["ModelQty"] ?? "1");
                string deviceModel = ConfigurationManager.AppSettings["ModelStartAddress"] ?? string.Empty;
                if (modelQty > 1 && deviceModel.Length > 1)
                {
                    string prefix = Regex.Replace(deviceModel, @"[^a-zA-Z]", "");
                    int number = Convert.ToInt32(Regex.Replace(deviceModel, @"[^0-9]", ""));

                    for (int i = 0; i < modelQty; i++)
                    {
                        var plcVar = new PlcVariable(interfaceIndex++, $"Model{i + 1}", $"{prefix}{number++}");
                        ReadDevices.Add(plcVar);
                    }
                }

                CheckPlcAddress(interfaceIndex++, "TriggerAddress", true);
                CheckPlcAddress(interfaceIndex++, "MbbTriggerAddress", true);
                CheckPlcAddress(interfaceIndex++, "ReadyAddress", true);

                #endregion Read Configuration

                #region Write Configuration

                interfaceIndex = 0;
                WriteDevices = new List<PlcVariable>();

                CheckPlcAddress(interfaceIndex++, "AnomalyOnAddress", false);
                CheckPlcAddress(interfaceIndex++, "AnomalyOffAddress", false);

                #endregion Write Configuration
                
                readValues = new int[ReadDevices.Count];

                IsConfigurationSuccess = true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Exception on {System.Reflection.MethodBase.GetCurrentMethod().Name} >>> {ex.Message}\r\n{ex.StackTrace}");
                IsConfigurationSuccess = false;
            }
            finally { }
        }

        public override string StartInteface()
        {
            string returnMessage = string.Empty;
            try
            {
                if (CheckConnectionInfo())
                {
                    if (OpenConnection() == 0)
                    {
                        IsAlive = true;
                        returnMessage = $"Modbus connection is success";
                        Logger.Info(returnMessage);
                    }
                    else
                    {
                        returnMessage = "Modbus connection is Failed";
                        Logger.Error(returnMessage);
                    }
                }
                else
                {
                    returnMessage = $"PLC connection infomation is wrong";
                    Logger.Error(returnMessage);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Exception on {System.Reflection.MethodBase.GetCurrentMethod().Name} >>> {ex.Message}\r\n{ex.StackTrace}");
                returnMessage = ex.Message;
            }
            finally { }

            return returnMessage;
        }

        private bool CheckConnectionInfo()
        {
            bool isChecked = false;
            try
            {
                string ModbusIP = Convert.ToString(ConfigurationManager.AppSettings["ModbusIP"] ?? "null");
                int ModbusPort = Convert.ToInt32(ConfigurationManager.AppSettings["ModbusPort"] ?? "502");
                byte ModbusSlaveID = Convert.ToByte(ConfigurationManager.AppSettings["ModbusSlaveID"] ?? "1");
                if (ModbusIP != null)
                {
                    isChecked = true;
                    address = ModbusIP;
                    port = ModbusPort;
                    slaveID = ModbusSlaveID;
                    Logger.Info($"The ModbusIP is proper: [{ModbusIP}]");
                }
                else
                {
                    Logger.Warn($"The ModbusIP is not proper: [{ModbusIP}]");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Exception on {System.Reflection.MethodBase.GetCurrentMethod().Name} >>> {ex.Message}\r\n{ex.StackTrace}");
            }
            finally { }

            return isChecked;
        }

        public override int OpenConnection()
        {
            tcpclient = new TcpClient();
            tcpclient.BeginConnect(address, port, null, null);
            master = ModbusIpMaster.CreateIp(tcpclient);

            Logger.Info($"{System.Reflection.MethodBase.GetCurrentMethod().Name}(): Modbus Connection is Success");
            return 0;
        }

        public override int CloseConnection()
        {
            master.Dispose();
            
            Logger.Info($"{System.Reflection.MethodBase.GetCurrentMethod().Name}(): Modbus Close Connection is Success");

            return 0;
        }

        public override string ReadPlcValues()
        {
            try
            {
                if (ReadCoilsFromModbus(ref readValues) != 0)
                {
                    Logger.Error($"Modbus read error on {System.Reflection.MethodBase.GetCurrentMethod().Name}. device:[{DeviceRandomToRead.Replace("\n", "/")}]");
                    return $"Modbus Read Error: [{DeviceRandomToRead.Replace("\n", "/")}]";
                }
                else
                {
                    // 비동기로 보낸다
                    string json = JsonConvert.SerializeObject(ReadDevices);
                    Task.Run(() => HttpMessage.SendHttpMessage(json));

                    var itemReady = ReadDevices.Find(x => x.VarName == "ReadyAddress");
                    if (itemReady != null && itemReady.ReadValue == 0)
                    {
                        SetAPLCValueOn(itemReady.DeviceAddress);
                    }

                    if (PreviousRead != string.Join(" ", readValues))
                    {
                        PreviousRead = string.Join(" ", readValues);
                        Logger.Trace(PreviousRead);
                    }
                    return $"Modbus Read Success [{string.Join(" ", readValues)}]";
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Exception on {System.Reflection.MethodBase.GetCurrentMethod().Name} >>> {ex.Message}\r\n{ex.StackTrace}");
                return ex.Message;
            }
            finally { }
        }


        private void CheckPlcAddress(int index, string appSettingName, bool isRead = true)
        {
            try
            {
                string device = ConfigurationManager.AppSettings[appSettingName] ?? string.Empty;
                if (device != string.Empty)
                {
                    var plcVar = new PlcVariable(index, appSettingName, device);
                    if (isRead)
                    {
                        ReadDevices.Add(plcVar);
                    }
                    else
                    {
                        WriteDevices.Add(plcVar);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Exception on {System.Reflection.MethodBase.GetCurrentMethod().Name} >>> {ex.Message}\r\n{ex.StackTrace}");
            }
            finally { }
        }

        public int ReadCoilsFromModbus(ref int[] values)
        {
            int res = 0;

            for (int j = 0; j < 3; j++)
            {
                try
                {
                    lock (master)
                    {
                        int i = 0;
                        foreach (PlcVariable item in ReadDevices)
                        {
                            ushort modbusDevice = Convert.ToUInt16(item.DeviceAddress);
                            bool[] coilstatus = master.ReadCoils(slaveID, modbusDevice, 1);
                            if (coilstatus[0] == true)
                                item.ReadValue = 1;
                            else
                                item.ReadValue = 0;
                            values[i++] = item.ReadValue;
                        }

                        Logger.Debug($"Modbus Read Coils Success");
                        return res;
                    }
                    break;
                }
                catch (Exception ex)
                {
                    Logger.Error($"Exception on {System.Reflection.MethodBase.GetCurrentMethod().Name} >>> {ex.Message}\r\n{ex.StackTrace}");
                    res = -1;
                    StartInteface();
                }
                finally { }
            }

            return res;
        }

        public int SetAPLCValue(string device, bool value)
        {
		    if (device == string.Empty)
            {
                Logger.Error("Write Device is empty");
                return -1;
            }
			
            int res = 1;

            for (int i = 0; i < 3; i++)
            {
                try
                {
                    lock (master)
                    {
                        ushort modbusDevice = Convert.ToUInt16(device);
                        master.WriteSingleCoil(slaveID, modbusDevice, value);
                        Logger.Debug($"Modbus Write - device:{device}, value:{value}");
                    }
                    break;
                }
                catch (Exception ex)
                {
                    Logger.Error($"Exception on {System.Reflection.MethodBase.GetCurrentMethod().Name} >>> {ex.Message}\r\n{ex.StackTrace}");
                    res = -1;
                    StartInteface();    // write coil 디바이스가 계속 연결이 끊겨 exception 발생하여 connect+retry 추가함
                }
                finally { }
            }

            return res;
        }

        public override int SetAPLCValueOff(string device)
        {
            return SetAPLCValue(device, false);
        }

        public override int SetAPLCValueOn(string device)
        {
            return SetAPLCValue(device, true);
        }

        public async Task<int> AlarmBitOnAndOff(string deviceAlarm)
        {
            int returnVal = 0;
            try
            {
                returnVal = SetAPLCValueOn(deviceAlarm);
                await Task.Delay(500);
                returnVal = SetAPLCValueOff(deviceAlarm);
                Logger.Info($"Send Anomaly (On and Off) to device:{deviceAlarm}, result:{returnVal}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Exception on {System.Reflection.MethodBase.GetCurrentMethod().Name} >>> {ex.Message}\r\n{ex.StackTrace}");
            }
            return returnVal;
        }

        public async Task<int> ReleaseBitOnAndOff(string deviceRelease)
        {
            return await AlarmBitOnAndOff(deviceRelease);
        }

        public async Task<int> AlarmBitOn(string deviceAlarm)
        {
            int returnVal = SetAPLCValueOn(deviceAlarm);
            Logger.Info($"Send Anomaly to device:{deviceAlarm}, result:{returnVal}");
            await Task.Delay(10);
            return returnVal;
        }

        public async Task<int> ReleaseBitOn(string deviceRelease)
        {
            return await AlarmBitOn(deviceRelease);
        }

        public async Task<int> AlarmBitOff(string deviceAlarm)
        {
            int returnVal = SetAPLCValueOff(deviceAlarm);
            Logger.Info($"Send Release to device:{deviceAlarm}, result:{returnVal}");
            await Task.Delay(10);
            return returnVal;
        }

        public async Task<int> AlarmToggle(string device1, string device2)
        {
            int returnVal2 = SetAPLCValueOff(device2);
            int returnVal1 = SetAPLCValueOn(device1) * 10;
            Logger.Info($"Send Anomaly to device:{device1}, Release to device:{device2}, result:{returnVal1 + returnVal2}");
            await Task.Delay(10);
            return returnVal1 + returnVal2;
        }

        // SPY TEST
        public override int WriteToPLCRandomString(string device, string text)
        {
            int dummy = 0;
            return dummy;
        }
        public override string ReadFromPLCRandomString(string device, int size)
        {
            string dummy = "";
            return dummy;
        }
    }
}