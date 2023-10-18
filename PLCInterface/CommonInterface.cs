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
    }
}