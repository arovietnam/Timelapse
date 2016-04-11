using System;
using System.Configuration;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Threading;
using EvercamV2;
using BLL.Dao;
using BLL.Entities;
using BLL.Common;

namespace TimelapseAppService
{
    public class Executor
    {
        private static Dictionary<int, TimelapseProcessInfo> _timelapseInfos;
        private static string FilePath = Settings.BucketUrl + Settings.BucketName;
        bool isServiceRunning = true;
        public string TimelapserExePath;

        public void Execute()
        {
            Evercam Evercam = new Evercam(Settings.EvercamClientID, Settings.EvercamClientSecret, Settings.EvercamClientUri);

            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);
            _timelapseInfos = new Dictionary<int, TimelapseProcessInfo>();

            // cleaning, if there is any previous garbage
            Shutdown("all");

            Utils.FileLog("Service execution started...");

            while (isServiceRunning)
            {
                List<Timelapse> timelapses; //= new List<Timelapse>();
                try
                {
                    timelapses = TimelapseDao.GetList(null, null);
                }
                catch(Exception x)
                {
                    Utils.FileLog("Error fetching timelapses:" + x.Message);
                    Thread.Sleep(1000 * 10);        // 10 seconds
                    continue;
                }

                if (timelapses == null)
                {
                    Utils.FileLog("Timelapses could not be fetched !");
                    Thread.Sleep(1000 * 15);        // 15 seconds
                    continue;
                }

                if (timelapses.Count == 0)
                {
                    Utils.FileLog("No timelapse found !");
                    Thread.Sleep(1000 * 15);        // 15 seconds
                    continue;
                }

                string hello = "Found " + timelapses.Count + " timelapses";
                Console.WriteLine(hello);
                Utils.FileLog(hello);

                foreach (Timelapse timelapse in timelapses)
                {
                    try
                    {
                        if (string.IsNullOrEmpty((TimelapserExePath = CopyTimelapser(timelapse.ID))))
                        {
                            Utils.FileLog("Skipping timelapse... unable to create copy of Timelapser.exe");
                            continue;
                        }
                        
                        int pid = Utils.TimelapseRunning(timelapse.ID);
                        if (timelapse.Status == (int)TimelapseStatus.Paused)
                        {
                            if (pid > 0)
                                Utils.KillProcess(pid, timelapse.ID);
                            Utils.FileLog("Timelapse Paused: " + timelapse.ID);
                            continue;
                        }
                        if (pid == 0 && Utils.StartTimelapse(timelapse)) {
                            StartTimelapser(timelapse);
                            TimelapseDao.UpdateStatus(timelapse.Code, (TimelapseStatus)timelapse.Status, timelapse.StatusTag, timelapse.TimeZone);
                        }
                        else if (pid > 0 && Utils.StopTimelapse(timelapse))
                        {
                            Utils.KillProcess(pid, timelapse.ID);
                            TimelapseDao.UpdateStatus(timelapse.Code, (TimelapseStatus)timelapse.Status, timelapse.StatusTag, timelapse.TimeZone);
                        }
                        else if (pid < 0)   // halted process id
                        {
                            Utils.FileLog("Kill: Timelapse#" + timelapse.ID);
                            Utils.KillProcess(pid * -1, timelapse.ID);
                        }
                        else
                        {
                            Utils.FileLog("Exit Timelapse without any action: " + timelapse.ID);
                        }
                    }
                    catch (Exception x)
                    {
                        Utils.FileLog("Executor.CheckRequests Error (" + timelapse.ID + "): " + x.Message);
                    }
                }

                Thread.Sleep(1000 * 60 * Settings.RecheckInterval);     // RecheckInterval in minutes
                
                //CheckStartDeletion();
            }
        }

        private void StartTimelapser(Timelapse timelapse)
        {
            try
            {
                // tests if camera details are available from Evercam before starting its recorder
                Evercam.SANDBOX = Settings.EvercamSandboxMode;
                Evercam evercam = new Evercam(Settings.EvercamClientID, Settings.EvercamClientSecret, Settings.EvercamClientUri);
                if (!string.IsNullOrEmpty(timelapse.OauthToken))
                    evercam = new Evercam(timelapse.OauthToken);

                Camera camera = evercam.GetCamera(timelapse.CameraId);

                // if camera found then start its process
                if (camera.IsOnline || !isCreatedHls(timelapse.ID, camera.ID))
                {
                    ProcessStartInfo process = new ProcessStartInfo(TimelapserExePath, timelapse.ID.ToString());
                    process.UseShellExecute = true;
                    process.WindowStyle = ProcessWindowStyle.Hidden;    //ProcessWindowStyle.Normal;
                    
                    Process currentProcess = Process.Start(process);

                    //currentProcess.PriorityClass = ProcessPriorityClass.Idle;
                    //currentProcess.Refresh();

                    TimelapseProcessInfo tpi = new TimelapseProcessInfo();
                    tpi.ProcessId = currentProcess.Id;
                    tpi.IsResponding = true;
                    tpi.NextRun = Utils.SQLMinDate;
                    tpi.Interval = timelapse.SnapsInterval;

                    if (_timelapseInfos.ContainsKey(timelapse.ID))
                        _timelapseInfos[timelapse.ID] = tpi;
                    else
                        _timelapseInfos.Add(timelapse.ID, tpi);
                    Utils.FileLog("Start Timelapse: " + timelapse.ID);
                }
                else
                {
                    TimelapseDao.UpdateStatus(timelapse.Code, TimelapseStatus.Failed, "Camera not accessible", timelapse.TimeZone);
                    Utils.FileLog("Executor.StartTimelapser(" + timelapse.ID + ") Camera (" + camera.ID + ") is Offline at Evercam (" + timelapse.Title + ")");
                }
            }
            catch (Exception x)
            {
                if (x.Message.ToLower().Contains("not found")) 
                {
                    TimelapseDao.UpdateStatus(timelapse.Code, TimelapseStatus.NotFound, "Camera details Could not be retrieved from Evercam", timelapse.TimeZone);
                    Utils.FileLog("Executor.StartTimelapser(" + timelapse.ID + ") Error: Could not get camera (" + timelapse.CameraId
                        + ") details from Evercam (" + timelapse.Title + ")");
                }
                else if (x.Message.ToLower().Contains("not exist"))
                {
                    TimelapseDao.UpdateStatus(timelapse.Code, TimelapseStatus.NotFound, "Camera details Could not be retrieved from Evercam", timelapse.TimeZone);
                    Utils.FileLog("Executor.StartTimelapser(" + timelapse.ID + ") Error: Camera (" + timelapse.CameraId
                        + ") does not exist at Evercam (" + timelapse.Title + ")");
                }
                else
                {
                    TimelapseDao.UpdateStatus(timelapse.Code, TimelapseStatus.Failed, "Camera not accessible", timelapse.TimeZone);
                    Utils.FileLog("Executor.StartTimelapser(" + timelapse.ID + ") (" + timelapse.Title + ") Error: " + x.Message);
                }
            }
        }

        private void CheckStartDeletion()
        {
            try
            {
                Process[] processlist = Process.GetProcesses();
                foreach (Process process in processlist)
                {
                    if (process.ProcessName.ToLower().Equals("deletion.exe"))
                    {
                        if (!process.HasExited && !process.Responding)
                            Utils.KillProcess(process.Id, 0);
                    }
                }

                TimeSpan span = DateTime.UtcNow.Subtract(Program.LastDeletion);
                if (span.Hours > Settings.DeletionCheckHours)
                {
                    Program.LastDeletion = DateTime.UtcNow.AddMinutes(1);

                    ProcessStartInfo process = new ProcessStartInfo(Settings.DeletionExePath);
                    process.UseShellExecute = true;
                    process.WindowStyle = ProcessWindowStyle.Normal;
                    Process deletionProcess = Process.Start(process);
                }
            }
            catch(Exception x)
            {
                Utils.FileLog("CheckStartDeltion Error: " + x.Message);
            }
        }

        protected string CopyTimelapser(int id)
        {
            string ExeFile = Path.Combine(Settings.TimelapseExePath, "Timelapser.exe");
            string ConfigFile = Path.Combine(Settings.TimelapseExePath, "Timelapser.exe.config");
            string PathDest = Path.Combine(Settings.TimelapseExePath, "timelapser_" + id + ".exe");
            string ConfigDest = Path.Combine(Settings.TimelapseExePath, "timelapser_" + id + ".exe.config");
            try
            {
                // if already exists (and process is running) 
                // then kill the process and delete its exe
                if (File.Exists(PathDest) && !_timelapseInfos.ContainsKey(id))
                {
                    Utils.KillProcess(Utils.ProcessRunning("timelapser_" + id), 0);
                    Utils.KillProcess(Utils.ProcessRunning("ffmpeg_" + id), 0);

                    //retry
                    if (Utils.ProcessRunning("timelapser_" + id) > 0)
                        Utils.KillProcess(Utils.ProcessRunning("timelapser_" + id), 0);
                    if (Utils.ProcessRunning("ffmpeg_" + id) > 0)
                        Utils.KillProcess(Utils.ProcessRunning("ffmpeg_" + id), 0);
                    try
                    {
                        File.Delete(PathDest);
                        File.Delete(ConfigDest);

                        File.Copy(ExeFile, PathDest, true);
                        File.Copy(ConfigFile, ConfigDest, true);
                    }
                    catch (Exception x)
                    {
                        Utils.FileLog("CopyTimelapser(" + id + ") Error in File.Delete/File.Copy: " + x.Message);
                    }
                }
                else if (!File.Exists(PathDest) && !_timelapseInfos.ContainsKey(id))
                {
                    File.Copy(ExeFile, PathDest, true);
                    File.Copy(ConfigFile, ConfigDest, true);
                }
            }
            catch (Exception x)
            {
                Utils.FileLog("CopyTimelapser(" + id + ") Error: " + x.Message);
                return "";
            }
            return PathDest;
        }

        protected bool isCreatedHls(int timelapse_id, string camera_exid)
        {
            string downPath = Path.Combine(FilePath, camera_exid, timelapse_id.ToString(), "images");
            string tsPath = Path.Combine(FilePath, camera_exid, timelapse_id.ToString(), "ts");

            if (Directory.Exists(downPath) && Directory.Exists(tsPath))
            {
                DirectoryInfo imagesDirectory = new DirectoryInfo(downPath);
                int imagesCount = imagesDirectory.GetFiles("*.jpg").Length;
                DirectoryInfo ts = new DirectoryInfo(tsPath);
                int hasTsFiles = ts.GetFiles("*.*").Length;
                if (hasTsFiles == 0 && imagesCount > 0)
                {
                    return false;
                }
            }
            else if (Directory.Exists(downPath) && !Directory.Exists(tsPath)) {
                DirectoryInfo imagesDirectory = new DirectoryInfo(downPath);
                int imagesCount = imagesDirectory.GetFiles("*.jpg").Length;
                if (imagesCount > 0)
                {
                    return false;
                }
            }

            return true;
        }

        void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = e.ExceptionObject as Exception;
            Utils.FileLog("UnhandledException Error: " + ex.ToString());
            StopExecution();
            Environment.ExitCode = 10;
        }

        public void StopExecution()
        {
            isServiceRunning = false;
            //// also set service properties to run Shutdown.exe all 
            //// as external program in case of service failure
            Shutdown("all");
        }

        protected void Shutdown(string param)
        {
            try
            {
                Utils.FileLog("TimelapseAppService Shutdown " + param);
                ProcessStartInfo process = new ProcessStartInfo(Settings.ShutdownProcessPath, param);
                process.UseShellExecute = true;
                process.WindowStyle = ProcessWindowStyle.Hidden;

                Process currentProcess = Process.Start(process);
                //currentProcess.WaitForExit();
            }
            catch (Exception x)
            {
                Utils.FileLog("TimelapseAppService Shutdown Error: " + x.Message);
            }
        }

        public class TimelapseProcessInfo
        {
            public TimelapseProcessInfo()
            {
                NextRun = Utils.SQLMinDate;
            }

            public DateTime NextRun { get; set; }
            public bool IsResponding { get; set; }
            public int Interval { get; set; }
            public int ProcessId { get; set; }
        }
    }
}
