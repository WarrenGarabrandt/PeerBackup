namespace PeerBackup.Data
{
    public class WorkerReport
    {
        public string LogMessage;
        public string LogWarning;
        public string LogError;
        public ServiceControl.ServiceState ServiceState;
        public bool SetServiceState;
    }
}
