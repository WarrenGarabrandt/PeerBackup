using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;
using System.Diagnostics;
using Microsoft.Win32.SafeHandles;
using System.IO;
using System.Threading;
using PeerBackup.Data;

namespace PeerBackupService
{

    public static class EntryPoint
    {
        [DllImport("kernel32.dll",
            EntryPoint = "GetStdHandle",
            SetLastError = true,
            CharSet = CharSet.Auto,
            CallingConvention = CallingConvention.StdCall)]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll")]
        private static extern void SetStdHandle(UInt32 nStdHandle, IntPtr handle);

        [DllImport("kernel32.dll",
            EntryPoint = "AllocConsole",
            SetLastError = true,
            CharSet = CharSet.Auto,
            CallingConvention = CallingConvention.StdCall)]
        private static extern int AllocConsole();

        private const UInt32 StdOutputHandle = 0xFFFFFFF5;
        private const int STD_OUTPUT_HANDLE = -11;
        private const int MY_CODE_PAGE = 437;
        /// <summary>
        /// The Main Thread: This is where the Service is Run.
        /// </summary>
        static void Main(String[] args)
        {
            if (Environment.UserInteractive)
            {
                AllocConsole();
                IntPtr stdHandle = GetStdHandle(STD_OUTPUT_HANDLE);
                SafeFileHandle safeFileHandle = new SafeFileHandle(stdHandle, true);
                FileStream fileStream = new FileStream(safeFileHandle, FileAccess.Write);
                Encoding encoding = System.Text.Encoding.GetEncoding(MY_CODE_PAGE);
                StreamWriter standardOutput = new StreamWriter(fileStream, encoding);
                standardOutput.AutoFlush = true;
                Console.SetOut(standardOutput);

                Console.WriteLine("Running interactively. To install and run as a service, use installutil.exe.");
                standardOutput.Flush();
                PeerBackupService svc = new PeerBackupService(interactive: true);
                svc.RunningInteractively = true;
                svc.StartService(args);
                while (svc.ServiceRunning)
                {
                    Thread.Sleep(10);
                }
            }
            else
            {
                ServiceBase.Run(new PeerBackupService());
            }
        }
    }

    public class PeerBackupService : ServiceBase
    {
        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool SetServiceStatus(System.IntPtr handle, ref ServiceControl.ServiceStatus serviceStatus);

        // main worker for our service
        BackgroundWorker worker;

        public bool RunningInteractively = false;
        public bool ServiceRunning = false;
        public PeerBackupService(bool interactive = false)
        {
            RunningInteractively = interactive;
            this.ServiceName = "PeerBackupService";
            this.EventLog.Log = "Application";

            if (!interactive)
            {
                ((ISupportInitialize)(this.EventLog)).BeginInit();
                if (!EventLog.SourceExists(this.EventLog.Source))
                {
                    EventLog.CreateEventSource(this.EventLog.Source, this.EventLog.Log);
                }
                ((ISupportInitialize)(this.EventLog)).EndInit();
            }
            this.CanHandlePowerEvent = true;
            this.CanHandleSessionChangeEvent = true;
            this.CanShutdown = true;
            this.CanStop = true;
        }

        /// <summary>
        /// Dispose of objects that need it here.
        /// </summary>
        /// <param name="disposing">Whether
        ///    or not disposing is going on.</param>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }

        public void StartService(string[] args)
        {
            OnStart(args);
        }

        public void StopService()
        {
            OnStop();
        }

        /// <summary>
        /// OnStart(): Put startup code here
        ///  - Start threads, get inital data, etc.
        /// </summary>
        /// <param name="args"></param>
        protected override void OnStart(string[] args)
        {
            ServiceRunning = true;
            // Update the service state to Start Pending.
            ServiceControl.ServiceStatus serviceStatus = new ServiceControl.ServiceStatus();
            serviceStatus.dwCurrentState = ServiceControl.ServiceState.SERVICE_START_PENDING;
            serviceStatus.dwWaitHint = 100000;
            SetServiceStatus(this.ServiceHandle, ref serviceStatus);

            if (RunningInteractively)
            {
                Console.Write("PeerBackup Service Starting.\r\n");
            }
            else
            {
                this.EventLog.WriteEntry("PeerBackup Service Starting.");
            }
            worker = new BackgroundWorker()
            {
                WorkerReportsProgress = true,
                WorkerSupportsCancellation = true
            };
            worker.DoWork += Worker_DoWork;
            worker.ProgressChanged += Worker_ProgressChanged;
            worker.RunWorkerCompleted += Worker_RunWorkerCompleted;
            worker.RunWorkerAsync();
            base.OnStart(args);
        }

        /// <summary>
        /// OnStop(): Put your stop code here
        /// - Stop threads, set final data, etc.
        /// </summary>
        protected override void OnStop()
        {
            // Update the service state to Stop Pending.
            ServiceControl.ServiceStatus serviceStatus = new ServiceControl.ServiceStatus();
            serviceStatus.dwCurrentState = ServiceControl.ServiceState.SERVICE_STOP_PENDING;
            serviceStatus.dwWaitHint = 100000;
            SetServiceStatus(this.ServiceHandle, ref serviceStatus);
            if (RunningInteractively)
            {
                Console.Write("PeerBackup Stopping.\r\n");
            }
            else
            {
                this.EventLog.WriteEntry("PeerBackup Service Stopping.");
            }
            worker.CancelAsync();
            ServiceRunning = false;

        }

        ///// <summary>
        ///// OnPause: Put your pause code here
        ///// - Pause working threads, etc.
        ///// </summary>
        //protected override void OnPause()
        //{
        //    this.EventLog.WriteEntry("PeerBackup Service Pausing.");
        //    base.OnPause();
        //}

        ///// <summary>
        ///// OnContinue(): Put your continue code here
        ///// - Un-pause working threads, etc.
        ///// </summary>
        //protected override void OnContinue()
        //{
        //    base.OnContinue();
        //}

        /// <summary>
        /// OnShutdown(): Called when the System is shutting down
        /// - Put code here when you need special handling
        ///   of code that deals with a system shutdown, such
        ///   as saving special data before shutdown.
        /// </summary>
        protected override void OnShutdown()
        {
            if (RunningInteractively)
            {
                Console.Write("PeerBackup Shutdown.\r\n");
            }
            else
            {
                this.EventLog.WriteEntry("PeerBackup Service Shutdown.");
            }
            OnStop();
            base.OnShutdown();
        }

        /// <summary>
        /// OnCustomCommand(): If you need to send a command to your
        ///   service without the need for Remoting or Sockets, use
        ///   this method to do custom methods.
        /// </summary>
        /// <param name="command">Arbitrary Integer between 128 & 256</param>
        protected override void OnCustomCommand(int command)
        {
            //  A custom command can be sent to a service by using this method:
            //#  int command = 128; //Some Arbitrary number between 128 & 256
            //#  ServiceController sc = new ServiceController("NameOfService");
            //#  sc.ExecuteCommand(command);

            base.OnCustomCommand(command);
        }

        /// <summary>
        /// OnPowerEvent(): Useful for detecting power status changes,
        ///   such as going into Suspend mode or Low Battery for laptops.
        /// </summary>
        /// <param name="powerStatus">The Power Broadcast Status
        /// (BatteryLow, Suspend, etc.)</param>
        protected override bool OnPowerEvent(PowerBroadcastStatus powerStatus)
        {
            return base.OnPowerEvent(powerStatus);
        }

        /// <summary>
        /// OnSessionChange(): To handle a change event
        ///   from a Terminal Server session.
        ///   Useful if you need to determine
        ///   when a user logs in remotely or logs off,
        ///   or when someone logs into the console.
        /// </summary>
        /// <param name="changeDescription">The Session Change
        /// Event that occured.</param>
        protected override void OnSessionChange(SessionChangeDescription changeDescription)
        {
            base.OnSessionChange(changeDescription);
        }

        private void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            // Connect to our SQLite database to retrieve settings. If the database file doesn't exist, create and format it. 
            // Formatting will create reasonable defaults (though no cameras)
            try
            {
                if (!File.Exists(SQLiteDB.MainDBFile))
                {
                    worker.ReportProgress(0, new WorkerReport()
                    {
                        LogMessage = string.Format("No system database file. Creating a new one.")
                    });
                    WorkerReport FormatReport = SQLiteDB.FormatNewDatabase();
                    if (FormatReport != null)
                    {
                        worker.ReportProgress(0, FormatReport);
                        return;
                    }
                }
                WorkerReport InitReport = SQLiteDB.InitDatabase();
                if (InitReport != null)
                {
                    worker.ReportProgress(0, InitReport);
                    return;
                }
            }
            catch (Exception ex)
            {
                worker.ReportProgress(0, new WorkerReport()
                {
                    LogError = string.Format("Failed to start. {0}", ex.Message)
                });
                return;
            }

            // find out how many named pipes we are configured to accept


            //NamedPipeServerStream adminPipe = null;
            //if (RunningInteractively)
            //{
            //    // build a named pipe name based on the configured name in the sqlite database
            //    adminPipe = new NamedPipeServerStream("HyperionInteractiveUISession#OFISODNVSNJDFS", PipeDirection.InOut, 1);

            //}
            //List<TcpListener> Listeners = new List<TcpListener>();
            //List<SmtpClient> Clients = new List<SmtpClient>();
            try
            {
                //try
                //{
                //    StaticConfiguration.LoadSettings(Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location), "settings.inf"));
                //}
                //catch (Exception ex)
                //{
                //    throw new Exception(string.Format("Could not load settings. Inner Exception: {0}", ex.Message));
                //}
                //foreach (var ep in StaticConfiguration.EndPoints)
                //{
                //    try
                //    {
                //        TcpListener listener = new TcpListener(new IPAddress(ep.AddressBytes), ep.Port);
                //        Listeners.Add(listener);
                //    }
                //    catch (Exception ex)
                //    {
                //        worker.ReportProgress(0, new WorkerReport()
                //        {
                //            LogError = string.Format("IP Endpoint failed for {0}:{1}. Exception {2}", ep.Address, ep.Port, ex.Message)
                //        });
                //    }
                //}
                //if (Listeners.Count == 0)
                //{
                //    throw new Exception("No local IP End Points have been defined to listen on.");
                //}
                //foreach (var l in Listeners)
                //{
                //    l.Start();
                //}
                //worker.ReportProgress(0, new WorkerReport()
                //{
                //    LogMessage = "Started.",
                //    ServiceState = ServiceState.SERVICE_RUNNING,
                //    SetServiceState = true
                //});
                //while (!worker.CancellationPending)
                //{
                //    Thread.Sleep(10);
                //    foreach (var l in Listeners)
                //    {
                //        if (l.Pending())
                //        {
                //            try
                //            {
                //                SmtpClient client = new SmtpClient(l.AcceptTcpClient());
                //                Clients.Add(client);
                //                worker.ReportProgress(0, new WorkerReport()
                //                {
                //                    LogMessage = "Accepted SMTP Connection.",
                //                    ServiceState = ServiceState.SERVICE_RUNNING,
                //                    SetServiceState = true
                //                });
                //            }
                //            catch (Exception ex)
                //            {
                //                worker.ReportProgress(0, new WorkerReport()
                //                {
                //                    LogWarning = string.Format("Accepting Client Exception: {0}", ex.Message)
                //                });
                //            }
                //        }
                //    }
                //    foreach (var c in Clients)
                //    {
                //        // SmtpClient Clients has definition: public Queue<StatusMessage> Messages = new Queue<StatusMessage>();
                //        if (c.Messages.Count != 0)
                //        {
                //            var m = c.Messages.Dequeue();
                //            switch (m.Priority)
                //            {
                //                case MessagePriority.Information:
                //                    worker.ReportProgress(0, new WorkerReport()
                //                    {
                //                        LogMessage = m.Message
                //                    });
                //                    break;
                //                case MessagePriority.Warning:
                //                    worker.ReportProgress(0, new WorkerReport()
                //                    {
                //                        LogWarning = m.Message
                //                    });
                //                    break;
                //                case MessagePriority.Error:
                //                    worker.ReportProgress(0, new WorkerReport()
                //                    {
                //                        LogError = m.Message
                //                    });
                //                    break;
                //            }
                //        }
                //        if (c.Done)
                //        {
                //            // SmtpClient Clients has definition: public Queue<StatusMessage> Messages = new Queue<StatusMessage>();
                //            while (c.Messages.Count > 0)
                //            {
                //                var m = c.Messages.Dequeue();
                //                switch (m.Priority)
                //                {
                //                    case MessagePriority.Information:
                //                        worker.ReportProgress(0, new WorkerReport()
                //                        {
                //                            LogMessage = m.Message
                //                        });
                //                        break;
                //                    case MessagePriority.Warning:
                //                        worker.ReportProgress(0, new WorkerReport()
                //                        {
                //                            LogWarning = m.Message
                //                        });
                //                        break;
                //                    case MessagePriority.Error:
                //                        worker.ReportProgress(0, new WorkerReport()
                //                        {
                //                            LogError = m.Message
                //                        });
                //                        break;
                //                }
                //            }
                //            Clients.Remove(c);
                //            c.Dispose();
                //            break;
                //        }
                //    }
                //}
                Thread.Sleep(3000);
            }
            catch (Exception ex)
            {
                worker.ReportProgress(0, new WorkerReport()
                {
                    LogError = string.Format("Exception: {0}", ex.Message)
                });
            }
            finally
            {
                worker.ReportProgress(0, new WorkerReport()
                {
                    LogMessage = "Shutting Down."
                });
                //foreach (var l in Listeners)
                //{
                //    try
                //    {
                //        l.Stop();
                //    }
                //    catch { }
                //}
                //Listeners.Clear();
                //foreach (var c in Clients)
                //{
                //    try
                //    {
                //        c.Dispose();
                //    }
                //    catch { }
                //}
                //Clients.Clear();
            }
        }

        private void Worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (RunningInteractively)
            {
                Console.Write("PeerBackup Stopped.\r\n");
            }
            else
            {
                this.EventLog.WriteEntry("PeerBackup Service Stopped.");
            }
            // Update the service state to Stopped.
            ServiceControl.ServiceStatus serviceStatus = new ServiceControl.ServiceStatus();
            serviceStatus.dwCurrentState = ServiceControl.ServiceState.SERVICE_STOPPED;
            serviceStatus.dwWaitHint = 100000;
            SetServiceStatus(this.ServiceHandle, ref serviceStatus);

            if (RunningInteractively)
            {
                ServiceRunning = false;
            }
            base.OnStop();
        }

        private void Worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            WorkerReport rep = e.UserState as WorkerReport;
            if (rep != null)
            {
                if (rep.LogMessage != null)
                {
                    if (RunningInteractively)
                    {
                        Console.Write(string.Format("PeerBackup {0}\r\n", rep.LogMessage));
                    }
                    else
                    {
                        this.EventLog.WriteEntry(string.Format("PeerBackup Service {0}", rep.LogMessage), EventLogEntryType.Information);
                    }
                }
                if (rep.LogWarning != null)
                {
                    if (RunningInteractively)
                    {
                        Console.Write(string.Format("PeerBackup {0}\r\n", rep.LogWarning));
                    }
                    else
                    {
                        this.EventLog.WriteEntry(string.Format("PeerBackup Service {0}", rep.LogWarning), EventLogEntryType.Warning);
                    }
                }
                if (rep.LogError != null)
                {
                    if (RunningInteractively)
                    {
                        Console.Write(string.Format("PeerBackup {0}\r\n", rep.LogError));
                    }
                    else
                    {
                        this.EventLog.WriteEntry(string.Format("PeerBackup Service {0}", rep.LogError), EventLogEntryType.Error);
                    }
                }
                if (rep.SetServiceState)
                {
                    // Update the service state.
                    ServiceControl.ServiceStatus serviceStatus = new ServiceControl.ServiceStatus();
                    serviceStatus.dwCurrentState = rep.ServiceState;
                    serviceStatus.dwWaitHint = 100000;
                    SetServiceStatus(this.ServiceHandle, ref serviceStatus);
                }
            }
        }
    }
}

