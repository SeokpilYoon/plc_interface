using Newtonsoft.Json;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System;

namespace PLCInterface
{
    abstract class CommonInterface
    {
        public bool IsConfigurationSuccess { get; set; } = false;
        public bool IsAlive { get; set; } = false;

        public abstract string StartInterface();
        public abstract int OpenConnection();
        public abstract int CloseConnection();
        public abstract string ReadPlcValues();
        public abstract int SetAPLCValueOn(string device);
        public abstract int SetAPLCValueOff(string device);
        public abstract int WriteToPLCRandomString(string device, string text);
        public abstract string ReadFromPLCRandomString(string device, int size);
        public abstract int GetReadDevicesCount();

        public void SendManualTrigger(string varName, int value)
        {
            try
            {
                var messageTasks = new List<Task>();
                for (int j = 0; j < GlobalInfo.NumChannel; j++)
                {
                    PlcVariable item = new PlcVariable(0, varName, varName, j+1);
                    List<PlcVariable> items = new List<PlcVariable>();
                    item.ReadValue = value;
                    items.Add(item);
                    string json = JsonConvert.SerializeObject(items);

                    messageTasks.Add(Task.Run(() =>
                    {
                        HttpMessage.SendHttpMessage(json, j + 1);
                    }));
                    Thread.Sleep(10);   // 이 부분 없으면 꼬임(TODO: 쓰레드 간 변수 독립성 보장 필요)
                }

                Task t = Task.WhenAll(messageTasks);
                t.Wait();
            }
            catch (Exception ex)
            {
                Logger.Error($"Exception on {System.Reflection.MethodBase.GetCurrentMethod().Name} >>> {ex.Message}\r\n{ex.StackTrace}");
            }
            finally { }
        }
    }
}