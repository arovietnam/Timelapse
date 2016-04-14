﻿using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Globalization;
using System.Linq;
using System.Security.Permissions;
using System.Text;
using System.Threading;
using System.Net;
using System.IO;
using System.Diagnostics;
using System.Drawing;
using System.Collections;
using System.Data;
using Npgsql;
using System.Data.Odbc;
using System.Data.OleDb;
using EvercamV2;
using BLL.Common;

namespace EvercamMovieMaker
{
    [SecurityPermission(SecurityAction.Assert, UnmanagedCode = false)]
    public class MovieMaker
    {
        private static int _tries;
        private LogLine _logger;
        private Timer _timer;
        private Evercam _evercam;
        private ArrayList _arMovies = new ArrayList();
        private bool Isbusy { get; set; }
        private int _numberofFrames;
        //public string Prefix = Settings.BucketUrl + Settings.AwsBucketName + @"\";

        public MovieMaker(LogLine logger, Evercam evercam)
        {
            _logger = logger;
            _evercam = evercam;
            _tries = 0;
        }

        public void Start()
        {
            Isbusy = false;
            _timer = new Timer(StartTimer, "", 0, 1000 * 60 * 5);
        }

        protected void StartTimer(object state)
        {
            if (!Isbusy)
                CreateMovie();
        }

        protected void CreateMovie()
        {
            Console.WriteLine(DateTime.UtcNow + " Check pending archive.");
            _tries = 0;
            //Handle exception if failed to get pending id
            Archive archive = _evercam.GetPendingArchive();
            if (archive == null || string.IsNullOrEmpty(archive.ID))
            {
                return;
            }
            Camera cam = new Camera();
            try
            {
                //added condition to check movie and camera object must not empty
                cam = _evercam.GetCamera(archive.CameraId);
            }
            catch (Exception) { 
                updateArchive(_evercam, archive.CameraId, archive.ID, 0, ArchiveStatus.Failed);
                _logger("Camera not found." + archive.CameraId);
            }
            if (cam == null || string.IsNullOrEmpty(cam.ID))
            {
                return;
            }
            Isbusy = true;
            var tz = Utility.ToWindowsTimezone(cam.Timezone);
            //update movie status when we sure application completed after few necessory steps.
            updateArchive(_evercam, archive.CameraId, archive.ID, 0, ArchiveStatus.Processing);

            string archivePath = Path.Combine(Settings.BucketUrl, Settings.BucketName, cam.ID, "archives");
            if (!Directory.Exists(archivePath))
                Directory.CreateDirectory(archivePath);

            string images_directory = Path.Combine(archivePath, "images");
            if (!Directory.Exists(images_directory))
                Directory.CreateDirectory(images_directory);

            _logger("Started downloading to " + archivePath);
            
            try
            {
                DateTime fromDate = Utility.ToWindowsDateTime(archive.FromDate);
                fromDate = ConvertFromUtc(fromDate, Utility.ToWindowsTimezone(cam.Timezone));
                DateTime toDate = Utility.ToWindowsDateTime(archive.ToDate);
                toDate = ConvertFromUtc(toDate, Utility.ToWindowsTimezone(cam.Timezone));
                int total_frames = DownloadAllImages(_evercam, cam, fromDate, toDate, images_directory);
                DirectoryInfo imagesDirectory = new DirectoryInfo(images_directory);
                if (imagesDirectory.GetFiles("*.jpg").Length == 0)
                {
                    updateArchive(_evercam, archive.CameraId, archive.ID, total_frames, ArchiveStatus.Failed);
                    _logger("Camera: " + cam.ID + ", There is no image between given time period for movie ." + archive.ID);
                }
                else
                {
                    string mp4FileName = Path.Combine(archivePath, archive.ID + ".mp4");
                    string webmFileName = Path.Combine(archivePath, archive.ID + ".webm");

                    CreateVideoFile(mp4FileName, images_directory);
                    //CreateVideoFile(webmFileName, images_directory);

                    Console.WriteLine("Camera: " + cam.ID + ", " + DateTime.UtcNow + ": Completed video processing.");
                    updateArchive(_evercam, archive.CameraId, archive.ID, total_frames, ArchiveStatus.completed);
                }
            }
            catch (Exception ex)
            {
                updateArchive(_evercam, archive.CameraId, archive.ID, 0, ArchiveStatus.Failed);
                Console.WriteLine("Camera: " + cam.ID + ", Error:" + ex.Message);
            }
            Clean(images_directory);
            if (Isbusy)
            {
                //KillFfMpeg();
                Isbusy = false;
            }
        }

        public static DateTime ConvertFromUtc(DateTime dt, string timezone)
        {
            TimeZoneInfo tzi = GetTimeZoneInfo(timezone);
            return TimeZoneInfo.ConvertTimeFromUtc(dt, tzi);
        }

        public static TimeZoneInfo GetTimeZoneInfo(string tz)
        {
            TimeZoneInfo tzi = String.IsNullOrEmpty(tz) ? TimeZoneInfo.Utc : TimeZoneInfo.FindSystemTimeZoneById(tz);
            return tzi;
        }

        public static bool updateArchive(Evercam evercam, string camera_id, string archive_id, int total_frames, ArchiveStatus status)
        {
            try
            {
                ArchiveInfo archiveInfo = new ArchiveInfo();
                archiveInfo.CameraId = camera_id;
                archiveInfo.ID = archive_id;
                archiveInfo.Status = status;
                archiveInfo.Frames = total_frames;
                evercam.UpdateArchive(archiveInfo);
                return true;
            }
            catch (Exception ex) {
                return false;
            }
        }

        public static int DownloadAllImages(Evercam evercam, Camera camera, DateTime userFromDate, DateTime userToDate, string path)
        {
            byte[] data = null;
            int index = 0;
            long fromTimestamp = Utility.ToUnixTimestamp(userFromDate);
            long toTimestamp = Utility.ToUnixTimestamp(userToDate);
            List<Snapshot> snaps = evercam.GetSnapshots(camera.ID, fromTimestamp, toTimestamp, 10000, null);

            foreach (Snapshot s in snaps)
            {
                try
                {
                    Snapshot snap = evercam.GetSnapshot(camera.ID, s.CreatedAt.ToString(), true, 0);
                    DateTime datetime = Utility.ToWindowsDateTime(snap.CreatedAt);
                    DateTime snaptime = ConvertFromUtc(datetime, Utility.ToWindowsTimezone(camera.Timezone));
                    if (snap != null && snaptime >= userFromDate)
                    {
                        data = snap.ToBytes();

                        //if (timestamp == "true")
                        //    data = Utils.TimestampImage(data, snaptime.ToString(), Settings.WatermarkPostion);

                        Console.WriteLine("Camera: " + camera.ID + ", " + index + " : " + snap.CreatedAt + " = " + snaptime);

                        try
                        {
                            if (SaveFile(Path.Combine(path, index + ".jpg"), data))
                                index++;
                        }
                        catch (Exception x)
                        {
                            Console.WriteLine(" - ERR1 :" + x.Message);
                        }
                    }
                }
                catch (Exception x)
                {
                    Console.WriteLine(" - ERR2 :" + x.Message);
                }
            }
            return index;
        }
        
        /// <summary>
        /// Create compressed webm file
        /// </summary>
        /// <param name="sourceFileName">Source video path</param>
        /// <param name="outputFile">Output video path</param>
        public void ConvertAviToWebm(string sourceFileName, string outputFile)
        {
            var p = new Process
            {
                StartInfo =
                {
                    UseShellExecute = false,
                    FileName = Settings.FfmpegExePath,
                    Arguments =
                        "-i " + sourceFileName +
                        " -codec:v libvpx -quality good -cpu-used 4 -b:v 250k -qmin 10 -qmax 42 -maxrate 500k -bufsize 1000k -threads 2 -vf scale=-1:480 " +
                        outputFile
                }
            };
            //p.PriorityClass = ProcessPriorityClass.High;
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.WindowStyle = ProcessWindowStyle.Normal;
            p.Start();
            p.WaitForExit();
            if (p.ExitCode != 0)
            {
                //string error = p.StandardError.ReadToEnd();
                p.Dispose();
                throw new ApplicationException();
            }
            p.Dispose();
        }

        /// <summary>
        /// Create cpmpressed mp4 file
        /// </summary>
        /// <param name="sourceFileName">Source video file</param>
        /// <param name="outputFile">Output video path</param>
        public void CreateVideoFile(string outputFile, string images_directory)
        {
            var p = new Process
            {
                StartInfo =
                {
                    UseShellExecute = false,
                    FileName = Settings.FfmpegExePath,
                    Arguments =
                        "-r 24 -i " + images_directory + @"\%d.jpg -c:v libx264 -r 24 " +
                        " -profile:v main -preset slow -b:v 1000k -maxrate 1000k -bufsize 1000k -vf scale=-1:720 -pix_fmt yuv420p -y " + outputFile
                },
            };
            //p.PriorityClass = ProcessPriorityClass.High;
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.WindowStyle = ProcessWindowStyle.Normal;
            p.Start();
            p.WaitForExit();
            if (p.ExitCode != 0)
            {
                string error = p.StandardError.ReadToEnd();
                p.Dispose();
                throw new ApplicationException();
            }
            p.Dispose();
        }

        public string GetClipInfo(string movieName)
        {
            try
            {
                var p = new Process();
                string fileargs = " -i " + movieName + " ";

                p.StartInfo.UseShellExecute = false;
                p.StartInfo.RedirectStandardError = true;
                p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;

                p.StartInfo.FileName = Settings.FfmpegExePath;
                p.StartInfo.Arguments = fileargs;

                p.Start();
                StreamReader errorreader = p.StandardError;
                string result = errorreader.ReadToEnd();

                int index1 = result.IndexOf("Duration: ", StringComparison.Ordinal) + ("Duration: ").Length;
                //index2 = result.IndexOf(", start:");
                int index2 = index1 + 8;
                return result.Substring(index1, index2 - index1);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return "";
            }
        }

        /*public void EmbedTimestamp(List<MongoSnapshot> snapshots, ref int idx, string path, TimeZoneInfo tzi, int cameraId)
        {
            foreach (var s in snapshots)
            {
                var sourceFile = Prefix +
                                      SnapshotService.GetAmazonObjectName(s._id, cameraId, DateTimeUtils.ParseFormattedDT(s._id)).Replace("/",
                                                                                                                 @"\");
                var destFile = path + @"\frame" + idx.ToString("000000") + ".jpg";
                string timeStamp = TimeZoneInfo.ConvertTimeFromUtc(DateTimeUtils.ParseFormattedDT(s._id), tzi).ToString("MM/dd/yyyy HH:mm:ss");
                MarkImage(timeStamp, destFile, sourceFile);
                idx++;
            }
        }*/

        /*public void DownloadEmbedTimestamp(List<MongoSnapshot> snapshots, ref int idx, string path, Camera c)
        {
            var wc = new WebClient();
            int? hour = null;
            string serverIp = c.ServerIp;
            TimeZoneInfo tzi = DateTimeUtils.GetTimeZoneInfo(c.Timezone);
            string tempPath = path + "\\temp";
            foreach (var s in snapshots)
            {
                var utc = DateTimeUtils.ParseFormattedDT(s._id);
                if (!hour.HasValue || utc.Hour != hour)
                {
                    var info = MongoCameraHourInfoDao.GetCameraHourInfoById(c.Id, utc, c.ServerIp);
                    serverIp = info.Sip;
                    hour = utc.Hour;
                }
                string url = SnapshotService.GetSnapshotUrl(s._id, s.ID, c.Id,
                                                            DateTimeUtils.ParseFormattedDT(s._id),
                                                            (serverIp == Settings.CaptureServerIp)
                                                                ? "localhost"
                                                                : serverIp);
                url = url.Replace(serverIp, Servers.GetServerPrivateIp(serverIp));

                if (!Directory.Exists(tempPath))
                    Directory.CreateDirectory(tempPath);
                try
                {
                    wc.DownloadFile(url, tempPath + "\\temp.jpg");
                }
                catch (Exception ex)
                {
                    //do not throw
                    EmailService.SendException(ex);
                    SaveMovieLog(ex.Message, "MovieLog");
                    continue;
                }

                var destFile = path + @"\frame" + idx.ToString("000000") + ".jpg";
                string timeStamp = TimeZoneInfo.ConvertTimeFromUtc(DateTimeUtils.ParseFormattedDT(s._id), tzi).ToString("MM/dd/yyyy HH:mm:ss");
                MarkImage(timeStamp, destFile, tempPath + "\\temp.jpg");
                idx++;
            }
            //Clean(path + "\\temp");
        }*/

        /*public void UploadMovie(string movieFileName, Movie m, string serverIp)
        {
            string extension = Path.GetExtension(movieFileName);
            //string amazonMovieName = SnapshotService.GetMovieAmazonObjectName(m.CameraId, m.Id, extension);
            string amazonMovieName = SnapshotService.GetMovieAmazonObjectName(m.CameraId, m.Id, extension);
            //AmazonService.SaveFile(amazonMovieName, movieFileName);
            StorageService.UploadFile(SnapshotService.GetMovieftpUrl(amazonMovieName, serverIp), movieFileName);
        }*/

        public void Clean(string path)
        {
            Directory.Delete(path, true);
        }

        public void SaveMovieLog(string message, string filename)
        {
            var stw = new StreamWriter(Settings.TempPath + "\\" + filename + ".txt", true);
            stw.WriteLine(message);
            stw.Close();
        }

        public void MarkImage(string timeStamp, string filePath, string temppath)
        {
            string stamp = timeStamp;//YOUR time stamp
            var originalBitMap = new Bitmap(temppath);

            int orWidth = originalBitMap.Width;
            int orHeight = originalBitMap.Height;
            double newHeight = orHeight * 0.95833333;
            newHeight = Math.Round(newHeight);

            var newBitmap = new Bitmap(originalBitMap, new Size(orWidth, int.Parse(newHeight.ToString(CultureInfo.InvariantCulture))));
            var canvas = new Bitmap(orWidth, orHeight);

            Graphics g = Graphics.FromImage(canvas);
            g.DrawImage(newBitmap, 0, 0);
            var textPos = new Point { X = orWidth - 150, Y = int.Parse(newHeight.ToString(CultureInfo.InvariantCulture)) + 1 };

            Brush textBrush = new SolidBrush(Color.White);

            g.DrawString(stamp, new Font("Arial", 11, FontStyle.Regular), textBrush, textPos);

            canvas.Save(filePath, ImageFormat.Jpeg);

            g.Dispose();
            originalBitMap.Dispose();
            canvas.Dispose();
            newBitmap.Dispose();
        }

        private static void KillFfMpeg()
        {
            try
            {
                const string ffMpegName = "ffmpeg";
                const string menCoderName = "men";

                var list = Process.GetProcessesByName(ffMpegName).ToList();
                list.AddRange(Process.GetProcessesByName(menCoderName).ToList());
                foreach (var p in list)
                {
                    try
                    {
                        p.Kill();
                        p.WaitForExit();
                    }
                    catch (Exception) { }
                }

                //verify that process are killed
                var vlist = Process.GetProcessesByName(ffMpegName).ToList();
                vlist.AddRange(Process.GetProcessesByName(menCoderName).ToList());
                if (vlist.Count > 0 && _tries < 5)
                {
                    Console.WriteLine("Trying to close ffmpeg " + _tries);
                    _tries += 1;
                    KillFfMpeg();
                    //if (_tries == 5)
                    //    EmailService.SendEmail("Failed to stop FFMPEG",
                    //                           " Movie creator is free but ffmpeg.exe is running and five tries to kill ffmpeg completed but its not closed. Please close it manually.");
                }
            }
            catch (Exception)
            {

            }
        }
        

        public static bool SaveFile(string fileName, byte[] data)
        {
            if (data == null)
                return false;
            try
            {
                string path = fileName.Substring(0, fileName.LastIndexOf(@"\"));

                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);
                FileStream stream = new FileStream(fileName, FileMode.Create);
                stream.Write(data, 0, data.Length);
                stream.Close();
                stream.Dispose();
                return true;
            }
            catch (Exception x)
            {
                return false;
            }
        }

        private static void connect1()
        {
            string connectionString = String.Format("Provider=PostgreSQL OLE DB Provider;Data Source={0}:{1};" +
                "location={4};User ID={2};password={3};timeout=1000;",
                "ec2-176-34-237-141.eu-west-1.compute.amazonaws.com", "5682", "u6075i8hdo4gph",
                "pbnc4i32sj1r022fiu1pf8ebbhi", "d3vugqi5rr36kj");

            using (OleDbConnection connection = new OleDbConnection(connectionString))
            {
                // The insertSQL string contains a SQL statement that
                // inserts a new row in the source table.
                OleDbCommand command = new OleDbCommand("SELECT * FROM archives");

                // Set the Connection to the new OleDbConnection.
                command.Connection = connection;

                // Open the connection and execute the insert command.
                try
                {
                    connection.Open();
                    command.ExecuteReader();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
                // The connection is automatically closed when the
                // code exits the using block.
            }
        }
        private static void connect() {
            DataSet ds = new DataSet();
            DataTable dt = new DataTable();
            //postgres://u6075i8hdo4gph:pbnc4i32sj1r022fiu1pf8ebbhi@ec2-176-34-237-141.eu-west-1.compute.amazonaws.com:5682/d3vugqi5rr36kj
            // PostgeSQL-style connection string
            string connstring = String.Format("Server={0};Port={1};" +
                "User Id={2};Password={3};Database={4};Integrated Security=true;",
                "ec2-176-34-237-141.eu-west-1.compute.amazonaws.com", "5682", "u6075i8hdo4gph",
                "pbnc4i32sj1r022fiu1pf8ebbhi", "d3vugqi5rr36kj");
            // Making connection with Npgsql provider
            NpgsqlConnection conn = new NpgsqlConnection(connstring);
            conn.Open();
            // quite complex sql statement
            string sql = "SELECT * FROM archives";
            // data adapter making request from our connection
            NpgsqlDataAdapter da = new NpgsqlDataAdapter(sql, conn);
            // i always reset DataSet before i do
            // something with it.... i don't know why :-)
            ds.Reset();
            // filling DataSet with result from NpgsqlDataAdapter
            da.Fill(ds);
            // since it C# DataSet can handle multiple tables, we will select first
            Console.WriteLine(ds.Tables[0]);
            // connect grid to DataTable
            
            // since we only showing the result we don't need connection anymore
            conn.Close();
        }
    }

    public delegate void LogLine(string str);
}