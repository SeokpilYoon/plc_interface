using System;
using System.Threading;
using System.IO.Ports;
using System.Text;
using System.Security.RightsManagement;

namespace PLCInterface
{
    class HoneywellBarcodeInterface
    {
        static SerialPort serialPort;
        static bool threadRun = true;

        public HoneywellBarcodeInterface()
        {
            try
            {
                serialPort = new SerialPort();

                serialPort.ReadTimeout = 500;
                serialPort.WriteTimeout = 500;

                serialPort.PortName = GlobalInfo.HoneywellBarcodePort;
                serialPort.BaudRate = 115200;
                serialPort.DataBits = 8;
                serialPort.Parity = Parity.None;
                serialPort.StopBits = StopBits.One;

                serialPort.Open();
                Logger.Debug($"Serial Port Opened.");
            }
            catch (Exception ex)
            {
                Logger.Error($"Exception on {System.Reflection.MethodBase.GetCurrentMethod().Name} >>> {ex.Message}\r\n{ex.StackTrace}");
            }
            finally { }
        }

        public string ReadBarcode()
        {
            StringBuilder rx = new StringBuilder();
            int asciiValue = 0;

            try
            {
                while (true)
                {
                    try
                    {
                        asciiValue = serialPort.ReadChar();
                        //Logger.Debug($"Serial Port Read : {asciiValue}");

                        // ASCII 코드가 유효한지 확인
                        if (asciiValue >= 0 && asciiValue <= 127)
                        {
                            // ASCII 값을 문자로 변환하고 결과에 추가
                            char character = (char)asciiValue;
                            rx.Append(character);
                        }
                        else
                        {
                            Logger.Error($"Invalid value {rx} read from barcode reader. (0-127)");
                        }
                    }
                    catch (System.TimeoutException) // Timeout 시 정상 break
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Exception on loop in ReadBarcode >>> {ex}");
                        break;
                    }
                    Thread.Sleep(1);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Exception on {System.Reflection.MethodBase.GetCurrentMethod().Name} >>> {ex.Message}\r\n{ex.StackTrace}");
            }
            finally { }

            string rst = rx.ToString();

            if (rst != string.Empty)
            {
                GlobalInfo.HoneywellBarcodeString = rst;
                Logger.Debug($"Barcode is read. value : {GlobalInfo.HoneywellBarcodeString}");
            }
            return rst;
        }


        static void Read()
        {
            string rx = string.Empty;
            while (threadRun)
            {
                try
                {
                    rx = serialPort.ReadLine();

                    if (rx != string.Empty)
                    {
                        Logger.Debug($"Serial Port Read : {rx}");
                        // set model
                    }
                }
                catch (System.TimeoutException)
                {
                }
                Thread.Sleep(1);
            }
        }
    }
}