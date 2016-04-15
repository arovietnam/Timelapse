using System;
using System.IO;
using System.Net;

using System.Collections;
using System.Drawing;
using System.Configuration;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.Threading;
using BLL.Entities;
using BLL.Dao;
using BLL.Common;
using System.Text;

namespace Timelapser
{
    public class Recorder
    {
        public string FfmpegExePath;
        public string BashFile;
        public int[] chunkIndex = new int[] { 0, 0, 0 };
        bool fileError = false;
        int timeOutCount = 0;
        int timeOutLimit = Settings.TimeoutLimit;
        int index = 0;
        ArrayList intervals = new ArrayList { 5, 15, 30, 60 };
        ArrayList intervals_max = new ArrayList { 360, 720, 1440 };
        Size dimension = new Size(int.Parse(Settings.VideoWidth), int.Parse(Settings.VideoHeight));
        string MAX_RES = ConfigurationSettings.AppSettings["MaxResCameras"];

        private string log = "";
        protected Timelapse timelapse { get; set; }

        public Recorder(Timelapse timelapse)
        {
            this.timelapse = timelapse;
        }

        public void Start()
        {
            if (timelapse.ID == 0)
            {
                Console.WriteLine("Timelapse details could not be found.");
                Utils.TimelapseLog(timelapse, "Exiting... Timelapse details could not be found.");
                ExitProcess();
                return;
            }

            // recording images sequential
            RecordTimelapse();
        }

        private void RecordTimelapse()
        {
            int processId = Utils.TimelapseRunning(timelapse.ID);
            Utils.TimelapseLog(timelapse, "Recording Timelapse...");

            Process p = Process.GetProcessById(processId);
            if (p.Id > 0 && p.Id != Process.GetCurrentProcess().Id && !p.HasExited && !p.Responding)
            {
                Utils.TimelapseLog(timelapse, "Killing previous halted process#" + p.Id);
                Utils.KillProcess(p.Id, timelapse.ID);
            }
            else if (p.Id > 0 && p.Id != Process.GetCurrentProcess().Id && !p.HasExited && p.Responding)
            {
                Utils.TimelapseLog(timelapse, "EXIT: Timelapse recorder already running process#" + p.Id);
                ExitProcess();
            }

            while (true)
            {
                Stopwatch watch = Stopwatch.StartNew();
                DateTime utcBefore = DateTime.UtcNow;
                try
                {
                    //// GET THIS FROM API !!!
                    timelapse = TimelapseDao.Get(timelapse.Code);

                    if (timelapse.ID == 0)
                    {
                        Utils.TimelapseLog(timelapse, "EXIT: Timelapse.ID == 0");
                        ExitProcess();
                    }
                    if (timelapse.RecreateHls)
                    {
                        Utils.TimelapseLog(timelapse, "EXIT: Because user request recreate HLS. Timelapse.ID == " + timelapse.ID);
                        ExitProcess();
                    }
                    if (timelapse.Status == (int)TimelapseStatus.Stopped && !timelapse.IsRecording)
                    {
                        Utils.TimelapseLog(timelapse, "EXIT: Timelapse.Status == Stopped");
                        ExitProcess();
                    }
                    if (fileError)
                    {
                        Utils.TimelapseLog(timelapse, "EXIT: Error in creating video file");
                        ExitProcess();
                    }
                    
                    Program.WatermarkFile = timelapse.ID + ".png";
                    string mp4IdFileName = Path.Combine(Program.UpPath, timelapse.ID + ".mp4");
                    string mp4CodeFileName = Path.Combine(Program.UpPath, timelapse.Code + ".mp4");
                    string tempMp4FileName = Path.Combine(Program.TempPath, timelapse.Code + ".mp4");
                    string tempVideoFileName = Path.Combine(Program.TempPath, "temp" + timelapse.Code + ".mp4");
                    string baseMp4FileName = Path.Combine(Program.TempPath, "base" + timelapse.Code + ".mp4");
                    BashFile = Path.Combine(Program.UpPath, "build.sh");
                    DirectoryInfo imagesDirectory = new DirectoryInfo(Program.DownPath);

                    if (Utils.StopTimelapse(timelapse))
                    {
                        TimelapseDao.UpdateStatus(timelapse.Code, (TimelapseStatus)timelapse.Status, timelapse.StatusTag, timelapse.TimeZone);
                        ExitProcess();
                    }
                    Utils.TimelapseLog(timelapse, "Timelapser Initialized @ " + Utils.ConvertFromUtc(DateTime.UtcNow, timelapse.TimeZone) + " (" + timelapse.FromDT + "-" + timelapse.ToDT + ")");
                    int imagesCount = imagesDirectory.GetFiles("*.jpg").Length;
                    index = imagesCount;
                    string imageFile = DownloadSnapshot();
                    // timelapse recorder is just initializing
                    if (!Program.Initialized)
                    {
                        Utils.TimelapseLog(timelapse, "<<< Initialized: images_count:" + imagesCount + ", Interval:" + timelapse.SnapsInterval + ", Snapshot Count:" + timelapse.SnapsCount);
                        DirectoryInfo ts = new DirectoryInfo(Program.TsPath);
                        int hasTsFiles = ts.GetFiles("*.*").Length;
                        if (hasTsFiles == 0 && imagesCount > 25)
                        {
                            CreateVideoChunks(BashFile);
                            Utils.TimelapseLog(timelapse, "Initial Stream <<< CreateVideoChunks");
                        }
                        else if (hasTsFiles == 0 && imagesCount > 0 && imagesCount < 25)
                        {
                            string sourceFile = Path.Combine(Program.DownPath, (index - 1) + ".jpg");
                            for (int i = index; i < 24; i++)
                            {
                                File.Copy(sourceFile, Path.Combine(Program.DownPath, i + ".jpg"), true);
                            }
                            if (imagesCount == 24)
                            {
                                CreateVideoChunks(BashFile);
                                imagesCount = imagesDirectory.GetFiles("*.jpg").Length;
                                index = imagesCount;
                            }
                            else
                            {
                                CreateVideoChunks(BashFile, false);
                                for (int i = index; i < 24; i++)
                                {
                                    File.Delete(Path.Combine(Program.DownPath, i + ".jpg"));
                                }
                            }
                            Utils.TimelapseLog(timelapse, "Initial Stream one repeated image<<< CreateVideoChunks");
                            
                        }
                        else if (CalculateChunckCreateTime(imagesCount, timelapse.SnapsInterval, timelapse.SnapsCount))
                        {
                            chunkIndex = GetTsFileIndex(Program.TsPath);
                            CreateNewVideoChunk(BashFile, timelapse.SnapsCount);
                            Utils.TimelapseLog(timelapse, "<<< CreateNewVideoChunk");
                        }
                        if (hasTsFiles > 0)
                        {
                            chunkIndex = GetTsFileIndex(Program.TsPath);
                            timelapse = TimelapseDao.Get(timelapse.Code);
                            Program.Initialized = true;
                        }
                    }
                    
                    if (!string.IsNullOrEmpty(imageFile))
                    {
                        if (intervals_max.Contains(timelapse.SnapsInterval) && index > 0 && index < 25)
                        {
                            if (Directory.Exists(Program.TsPath))
                                Directory.Delete(Program.TsPath, true);
                            Directory.CreateDirectory(Program.TsPath);
                            string sourceFile = Path.Combine(Program.DownPath, (index - 1) + ".jpg");
                            for (int i = index; i < 24; i++)
                            {
                                File.Copy(sourceFile, Path.Combine(Program.DownPath, i + ".jpg"), true);
                            }
                            if (imagesCount == 24)
                            {
                                CreateVideoChunks(BashFile);
                                imagesCount = imagesDirectory.GetFiles("*.jpg").Length;
                                index = imagesCount;
                            }
                            else
                            {
                                CreateVideoChunks(BashFile, false);
                                for (int i = index; i < 24; i++)
                                {
                                    File.Delete(Path.Combine(Program.DownPath, i + ".jpg"));
                                }
                            }
                            Utils.TimelapseLog(timelapse, "Add new image<<< CreateVideoChunks");
                        }
                        else if (CalculateChunckCreateTime(imagesCount, timelapse.SnapsInterval, timelapse.SnapsCount))
                        {
                            CreateNewVideoChunk(BashFile, timelapse.SnapsCount);
                            Utils.TimelapseLog(timelapse, "<<< CreateNewVideoChunk");
                        }
                    }
                    else
                    {
                        //// could not get an image from camera so retry after 15 seconds
                        if (timeOutCount >= timeOutLimit)
                        {
                            string log = "Camera not accessible (tried " + timeOutCount + " times) ";
                            Utils.TimelapseLog(timelapse, log);
                            TimelapseDao.UpdateStatus(timelapse.Code, TimelapseStatus.Failed, log, timelapse.TimeZone);
                            ExitProcess();
                        }
                        // wait for x seconds before next retry
                        Utils.TimelapseLog(timelapse, "Retry after " + Settings.RetryInterval + " seconds");
                        Thread.Sleep(TimeSpan.FromMilliseconds(Settings.RetryInterval * 1000));
                    }

                    DateTime utcAfter = utcBefore.AddMilliseconds(watch.ElapsedMilliseconds);
                    if (timelapse.SnapsInterval == 1)
                    {
                        if (utcAfter.Hour == utcBefore.Hour && utcAfter.Minute == utcBefore.Minute)
                        {
                            int wait = 60 - utcAfter.Second;
                            Utils.TimelapseLog(timelapse, "Wait for " + wait + " seconds");
                            Thread.Sleep(TimeSpan.FromMilliseconds(wait * 1000));
                        }
                    }
                    else
                    {
                        TimeSpan span = utcAfter.AddMinutes(timelapse.SnapsInterval).Subtract(utcAfter);
                        Utils.TimelapseLog(timelapse, "Wait for " + span.TotalMinutes + " minutes");
                        Thread.Sleep(TimeSpan.FromMilliseconds(span.TotalMinutes * 60 * 1000));
                    }
                }
                catch (Exception x)
                {
                    Utils.TimelapseLog(timelapse, "ERR: RecordTimelapse(): " + x);
                    TimelapseDao.UpdateStatus(timelapse.Code, TimelapseStatus.Failed, "Failed with error - " + x.Message, timelapse.TimeZone);
                    Console.WriteLine("RecordTimelapse Error: " + x.Message);
                }
            }
        }

        protected bool CalculateChunckCreateTime(int fileCount, int interval, int snapshotCount)
        {
            if (fileCount > snapshotCount)
            {
                if (interval == 1 && (fileCount - snapshotCount) >= ((24 * Program.chunkSize) - 1))
                    return true;
                else if (intervals.Contains(interval) && (fileCount - snapshotCount) >= 24)
                    return true;
                else if (intervals_max.Contains(interval) && (fileCount - snapshotCount) >= 24)
                    return true;
                else
                    return false;
            }
            else
                return false;
        }

        protected string DownloadSnapshot()
        {
            try
            {
                Program.Camera = Program.Evercam.GetCamera(timelapse.CameraId);
            }
            catch (Exception x)
            {
                Utils.TimelapseLog(timelapse, "ERR: DownloadSnapshot: " + x.ToString());
                if (x.Message.ToLower().Contains("not found"))
                    TimelapseDao.UpdateStatus(timelapse.Code, TimelapseStatus.NotFound, "Camera details could not be retreived from Evercam", timelapse.TimeZone);
                else if (x.Message.ToLower().Contains("not exist"))
                    TimelapseDao.UpdateStatus(timelapse.Code, TimelapseStatus.NotFound, "Camera details could not be retreived from Evercam", timelapse.TimeZone);
                else if (x.Message.ToLower().Contains("offline"))
                    TimelapseDao.UpdateStatus(timelapse.Code, TimelapseStatus.Failed, "Camera not accessible", timelapse.TimeZone);

                ExitProcess();
            }
            
            //// /1/images/0.jpg
            string tempfile = Path.Combine(Program.DownPath, index + ".jpg");
            byte[] data = null;
            try
            {
                // instead of trying for X times, just try once other wise fetch from recording
                // store and returns live snapshot on evercam
                var snap1 = Program.Evercam.CreateSnapshot(timelapse.CameraId, Settings.EvercamClientName, true);
                data = EvercamV2.Utility.ToBytes(snap1.Data);
                Utils.TimelapseLog(timelapse, "Image data retrieved from Camera");
            }
            catch (Exception x)
            {
                Utils.TimelapseLog(timelapse, "Exception: Image data retrieved from Camera. " + x.Message);
                EvercamV2.Snapshot snap = Program.Evercam.GetLatestSnapshot(timelapse.CameraId, true);
                data = snap.ToBytes();
                Utils.TimelapseLog(timelapse, "Latest Image data retrieved from Camera");
            }
            if (data != null && data.Length > 0)
            { }
            else
            {
                timeOutCount++;
                data = null;
                Utils.TimelapseLog(timelapse, "Image count not be retrieved from Camera");
            }

            if (data != null)
            {
                try
                {
                    if (Storage.SaveFile(tempfile, data))
                    {
                        //// should calculate original image ratio and give to ResizeImage function
                        //// will resize the image and rename as source file. e.g. code.jpg
                        
                        //// No more resizing... only create <CODE>.jpg file for poster from given file and logo
                        MakePoster(tempfile);
                        timeOutCount = 0;
                        index++;

                        if (timelapse.DateAlways && timelapse.TimeAlways) 
                        {
                            timelapse.Status = (int)TimelapseStatus.Processing;
                            TimelapseDao.UpdateStatus(timelapse.Code, TimelapseStatus.Processing, "Now recording...", timelapse.TimeZone);
                        }
                        else
                        {
                            timelapse.Status = (int)TimelapseStatus.Scheduled;
                            TimelapseDao.UpdateStatus(timelapse.Code, TimelapseStatus.Scheduled, "Recording on schedule...", timelapse.TimeZone);
                        }

                        TimelapseDao.UpdateLastSnapshot(timelapse.Code, DateTime.UtcNow);

                        Utils.TimelapseLog(timelapse, "DownloadSnapshot - Image saved " + tempfile);
                    }
                    else
                    {
                        tempfile = "";
                        timeOutCount++;
                        Utils.TimelapseLog(timelapse, "DownloadSnapshot - Image not retrieved");
                        if (timeOutCount >= timeOutLimit)
                        {
                            string log = "Camera not accessible (tried " + timeOutCount + " times) ";
                            TimelapseDao.UpdateStatus(timelapse.Code, TimelapseStatus.Failed, log, timelapse.TimeZone);
                            ExitProcess();
                        }
                    }
                }
                catch (Exception x)
                {
                    tempfile = "";
                    timeOutCount++;
                    Utils.TimelapseLog(timelapse, "Image could not be not saved from Camera - Error: " + x.ToString());
                    if (timeOutCount >= timeOutLimit)
                    {
                        string log = "Camera not accessible (tried " + timeOutCount + " times) ";
                        TimelapseDao.UpdateStatus(timelapse.Code, TimelapseStatus.Failed, log, timelapse.TimeZone);
                        ExitProcess();
                    }
                }
            }
            else
            {
                tempfile = "";
                timeOutCount++;
                Utils.TimelapseLog(timelapse, "Image could not be retrieved from Camera");
                if (timeOutCount >= timeOutLimit)
                {
                    string log = "Camera not accessible (tried " + timeOutCount + " times) ";
                    TimelapseDao.UpdateStatus(timelapse.Code, TimelapseStatus.Failed, log, timelapse.TimeZone);
                    ExitProcess();
                }
            }
            return tempfile;
        }

        public void CreateVideoChunks(string bashFile, bool update_info = true)
        {
            Utils.TimelapseLog(timelapse, ">>> CreateVideoChunks(" + bashFile + ")");
            RunBash(bashFile);
            if (update_info)
            {
                TimelapseVideoInfo info = UpdateVideoInfo("");
            }
        }

        protected void CreateNewVideoChunk(string bashFile, int start_number)
        {
            Utils.TimelapseLog(timelapse, ">>> CreateNewVideoChunk(" + bashFile + ", " + start_number + ")");
            string[] maxres = MAX_RES.Split(new char[] { ',' });
            CreateBashFile(bashFile, timelapse.FPS, timelapse.SnapsInterval, Program.DownPath, Program.TsPath, start_number);
            RunBash(bashFile);
            updateMenifiest(Program.TsPath, "low", chunkIndex[0]);
            updateMenifiest(Program.TsPath, "medium", chunkIndex[1]);
            updateMenifiest(Program.TsPath, "high", chunkIndex[2]);
            chunkIndex[0] = chunkIndex[0] + 1;
            chunkIndex[1] = chunkIndex[1] + 1;
            chunkIndex[2] = chunkIndex[2] + 1;
            TimelapseVideoInfo info = UpdateVideoInfo("");
        }

        protected static int[] GetTsFileIndex(string tsPath)
        {
            DirectoryInfo d = new DirectoryInfo(tsPath);
            int lowCount = d.GetFiles("low*.ts").Length;
            int mediumCount = d.GetFiles("medium*.ts").Length;
            int highCount = d.GetFiles("high*.ts").Length;
            return new int[] { lowCount, mediumCount, highCount };
        }

        protected void updateMenifiest(string tsPath, string fileName, int fileIndex)
        {
            string originalFile = Path.Combine(tsPath, fileName + ".m3u8");
            string tempFile = Path.Combine(tsPath, "temp-" + fileName + ".m3u8");
            
            using (StreamReader reader = new StreamReader(originalFile))
            {
                using (StreamWriter writer = new StreamWriter(tempFile))
                {
                    string line = reader.ReadLine();

                    while (!reader.EndOfStream)
                    {
                        writer.WriteLine(line);
                        line = reader.ReadLine();
                    }
                    writer.WriteLine("#EXT-X-DISCONTINUITY");
                    writer.WriteLine("#EXTINF:2.100000,");
                    writer.WriteLine(fileName + fileIndex + ".ts");
                    writer.WriteLine("#EXT-X-ENDLIST");
                }
            }
            File.Copy(tempFile, originalFile, true);
            File.Delete(tempFile);
        }

        protected void CreateBashFile(string bashFilePath, int frame_per_sec, int interval, string imagesPath, string tsPath, int start_number)
        {
            if (File.Exists(bashFilePath))
                File.Delete(bashFilePath);
            //if (interval <= 60)
            frame_per_sec = 24;
            imagesPath = imagesPath.Replace('\\', '/');
            tsPath = tsPath.Replace('\\', '/');
            var bash = new StringBuilder();
            bash.AppendLine("#!/bin/bash");
            var ffmpeg_command_480 = string.Format("ffmpeg -threads 1 -y -framerate {0} -start_number {3} -i {1}/%d.jpg -c:v libx264 -pix_fmt yuv420p -profile:v baseline -level 2.1 -maxrate 500K -bufsize 2M -crf 18 -r {0} -g 30 -s 480x270 {2}/low{4}.ts", frame_per_sec, imagesPath, tsPath, start_number, chunkIndex[0]);
            var ffmpeg_command_640 = string.Format("ffmpeg -threads 1 -y -framerate {0} -start_number {3} -i {1}/%d.jpg -c:v libx264 -pix_fmt yuv420p -profile:v baseline -level 3.1 -maxrate 1M -bufsize 3M -crf 18 -r {0} -g 72 -s 640x360 {2}/medium{4}.ts", frame_per_sec, imagesPath, tsPath, start_number, chunkIndex[1]);
            var ffmpeg_command_1280 = string.Format("ffmpeg -threads 1 -y -framerate {0} -start_number {3} -i {1}/%d.jpg -c:v libx264 -pix_fmt yuv420p -profile:v high -level 3.2 -maxrate 4M -crf 18 -r {0} -g 100 {2}/high{4}.ts", frame_per_sec, imagesPath, tsPath, start_number, chunkIndex[2]);
            bash.AppendLine(ffmpeg_command_480);
            bash.AppendLine(ffmpeg_command_640);
            bash.AppendLine(ffmpeg_command_1280);
            File.WriteAllText(bashFilePath, bash.ToString());
        }

        protected TimelapseVideoInfo CreateVideoFromImages(string output, string baseOutput)
        {
            Utils.TimelapseLog(timelapse, ">>> CreateVideoFromImages(" + output + ")");
            string[] maxres = MAX_RES.Split(new char[] {','});
            if (Array.IndexOf(maxres, timelapse.CameraId.ToLower()) > 0)
                RunProcess("-r " + timelapse.FPS + " -i " + Program.DownPath + @"\%00000d.jpg -c:v libx264 -r " + timelapse.FPS + " -profile:v main -pix_fmt yuv420p -y " + output);
            else
                RunProcess("-r " + timelapse.FPS + " -i " + Program.DownPath + @"\%00000d.jpg -c:v libx264 -r " + timelapse.FPS + " -profile:v main -preset slow -b:v 1000k -maxrate 1000k -bufsize 1000k -vf scale=-1:720 -pix_fmt yuv420p -y " + output);

            File.Copy(output, baseOutput, true);
            WatermarkVideo(baseOutput, output);

            TimelapseVideoInfo info = UpdateVideoInfo(output);
            Utils.TimelapseLog(timelapse, "<<< CreateVideoFromImages(" + output + ")");
            return info;
        }

        protected void GenerateVideoSingleImage(string output, string baseOutput, string imageFile)
        {
            Utils.TimelapseLog(timelapse, ">>> GenerateVideoSingleImage(" + output + ")");
            string[] maxres = MAX_RES.Split(new char[] { ',' });
            if (Array.IndexOf(maxres, timelapse.CameraId.ToLower()) > 0)
                RunProcess("-r " + timelapse.FPS + " -i " + imageFile + " -c:v libx264 -r " + timelapse.FPS + " -y -profile:v main -pix_fmt yuv420p " + output);
            else
                RunProcess("-r " + timelapse.FPS + " -i " + imageFile + " -c:v libx264 -r " + timelapse.FPS + " -y -profile:v main -preset slow -b:v 1000k -maxrate 1000k -bufsize 1000k -vf scale=-1:720 -pix_fmt yuv420p " + output);
            File.Copy(output, baseOutput, true);
            WatermarkVideo(baseOutput, output);

            UpdateVideoInfo(output);
            Utils.TimelapseLog(timelapse, "<<< GenerateVideoSingleImage(" + output + ")");
        }

        protected void ConcatenateVideoSingleImage(string mp4FileName, string tempMp4FileName, string baseMp4FileName, string tempVideoFileName, string imageFile)
        {
            try
            {
                /*Recovered*/
                Utils.TimelapseLog(timelapse, ">>> ConcatenateVideoSingleImage(" + mp4FileName + ")");
                string str = Path.Combine(Program.TempPath, this.timelapse.Code + ".txt");
                File.Delete(tempMp4FileName);
                File.Delete(tempVideoFileName);
                File.Delete(str);

                // create video file with single new snapshot
                string[] maxres = MAX_RES.Split(new char[] { ',' });
                if (Array.IndexOf(maxres, timelapse.CameraId.ToLower()) > 0)
                    RunProcess("-r " + timelapse.FPS + " -i " + imageFile + " -c:v libx264 -r " + timelapse.FPS + " -y -profile:v main -pix_fmt yuv420p " + tempVideoFileName);
                else
                    RunProcess("-r " + timelapse.FPS + " -i " + imageFile + " -c:v libx264 -r " + timelapse.FPS + " -y -profile:v main -preset slow -b:v 1000k -maxrate 1000k -bufsize 1000k -vf scale=-1:720 -pix_fmt yuv420p " + tempVideoFileName);

                // create text file that describes the files to be concatenated
                CreateConfigFile(baseMp4FileName, tempVideoFileName, str);

                // create a concatenated video file
                RunProcess("-f concat -i " + str + " -c copy " + tempMp4FileName);

                // saving a copy of original video as base
                File.Copy(tempMp4FileName, baseMp4FileName, true);

                WatermarkVideo(baseMp4FileName, mp4FileName);
                UpdateVideoInfo(mp4FileName);
                Utils.TimelapseLog(timelapse, "<<< ConcatenateVideoSingleImage(" + mp4FileName + ")");
            }
            catch (Exception x)
            {
                Utils.TimelapseLog(timelapse, "ERR: ConcatenateVideoSingleImage: " + x.ToString());
            }
        }

        private void WatermarkVideo(string input, string output)
        {
            string param = "";
            try
            {
                if (string.IsNullOrEmpty(timelapse.WatermarkImage))
                {
                    // just copy input file as it is to output location
                    File.Copy(input, output, true);
                    return;
                }
                else if (!File.Exists(Path.Combine(Program.TimelapseExePath, Program.WatermarkFile)))
                {
                    Utils.DoDownload(timelapse.WatermarkImage, Path.Combine(Program.TimelapseExePath, Program.WatermarkFile));
                    if (File.Exists(Path.Combine(Program.TimelapseExePath, Program.WatermarkFile)))
                        File.Copy(Path.Combine(Program.TimelapseExePath, Program.WatermarkFile), Path.Combine(Program.UpPath, Program.WatermarkFileName), true);
                }
                switch (timelapse.WatermarkPosition)
                {
                    case (int)WatermarkPosition.TopLeft:
                        param = "-i " + input + " -i " + Program.WatermarkFile + " -y -filter_complex \"overlay=10:10\" " + output;
                        break;
                    case (int)WatermarkPosition.TopRight:
                        param = "-i " + input + " -i " + Program.WatermarkFile + " -y -filter_complex \"overlay=(main_w-overlay_w)-10:10\" " + output;
                        break;
                    case (int)WatermarkPosition.BottomRight:
                        param = "-i " + input + " -i " + Program.WatermarkFile + " -y -filter_complex \"overlay=(main_w-overlay_w)-10:(main_h-overlay_h)-10\" " + output;
                        break;
                    case (int)WatermarkPosition.BottomLeft:
                        param = "-i " + input + " -i " + Program.WatermarkFile + " -y -filter_complex \"overlay=10:(main_h-overlay_h)-10\" " + output;
                        break;
                }
                RunProcess(param);
            }
            catch (Exception x)
            {
                Utils.TimelapseLog(timelapse, "ERR: Watermark Video: Process=" + param + Environment.NewLine + "Error: " + x.ToString());
            }
        }

        public TimelapseVideoInfo UpdateVideoInfo(string movieName)
        {
            string result = "";
            try
            {
                TimelapseVideoInfo info = new TimelapseVideoInfo();
                DirectoryInfo d = new DirectoryInfo(Program.DownPath);
                FileInfo[] filelist = d.GetFiles("*.jpg");
                int snapsCount = filelist.Length;
                if (snapsCount > 0)
                {
                    FileInfo file = new FileInfo(Path.Combine(Program.DownPath, (snapsCount - 1) + ".jpg"));
                    long fileSize = snapsCount * file.Length;

                    Image image = Image.FromFile(file.FullName);
                    string resolution = image.Width + "x" + image.Height;
                    info.FileSize = fileSize;
                    info.Resolution = resolution;
                    info.SnapsCount = snapsCount;
                    info.Duration = "00:00";
                    TimelapseDao.UpdateFileInfo(timelapse.Code, info);
                }
                return info;
            }
            catch (Exception ex)
            {
                Utils.TimelapseLog(timelapse, "ERR: UpdateVideoInfo(" + movieName + "): " + ex.ToString());
                // file is un-readable may be causing error like 'Invalid data found when processing input'
                // so move this bad copy of to /temp/ folder for backup and clean the space for new file
                string errVideoFileName = Path.Combine(Program.TempPath, "err" + timelapse.Code + ".mp4");
                if (File.Exists(errVideoFileName))
                    File.Delete(errVideoFileName);

                Utils.TimelapseLog(timelapse, "ERR: UpdateVideoInfo(" + movieName + "): " + Environment.NewLine + "Output: " + result);

                return new TimelapseVideoInfo();
            }
        }

        protected void RunBash(string parameters)
        {
            ProcessStartInfo start = new ProcessStartInfo();
            start.FileName = Path.Combine(Program.FfmpegCopyPath, "bash.exe");
            start.Arguments = parameters;
            start.UseShellExecute = false;
            start.RedirectStandardError = true;

            Process process = new Process();
            start.CreateNoWindow = true;
            process.StartInfo = start;
            process.Start();

            process.PriorityClass = ProcessPriorityClass.Idle;
            process.Refresh();

            string output = process.StandardError.ReadToEnd();
            
            try
            {
                if (!process.HasExited && process.Responding)
                    Utils.KillProcess(process.Id, 0);
            }
            catch (Exception x)
            {
                Utils.TimelapseLog(timelapse, "ERR: RunBash(" + parameters + ") " + x);
            }
        }

        protected string RunProcess(string parameters)
        {
            ProcessStartInfo start = new ProcessStartInfo();
            start.FileName = FfmpegExePath;
            start.Arguments = " -threads 1 " + parameters;
            start.UseShellExecute = false;
            start.RedirectStandardError = true;

            Process process = new Process();
            start.CreateNoWindow = true;
            process.StartInfo = start;
            process.Start();

            process.PriorityClass = ProcessPriorityClass.Idle;
            process.Refresh();

            string output = process.StandardError.ReadToEnd();
            
            try
            {
                if (!process.HasExited && process.Responding)
                    Utils.KillProcess(process.Id, 0);
                return "";
            }
            catch (Exception x)
            {
                Utils.TimelapseLog(timelapse, "ERR: RunProcess(" + parameters + ") " + x);
                return x.Message;
            }
        }

        protected void CreateConfigFile(string mp4File, string newMp4File, string txtFileName)
        {
            try
            {
                if (!File.Exists(txtFileName))
                {
                    using (FileStream f = File.Create(txtFileName))
                    {
                        f.Close();
                    }
                }
                using (StreamWriter logs = new StreamWriter(txtFileName, true))
                {
                    logs.WriteLine("# this is a comment");
                    logs.WriteLine("file '" + mp4File + "'");
                    logs.WriteLine("file '" + newMp4File + "'");
                }
            }
            catch (Exception x)
            {
                Utils.TimelapseLog(timelapse, "ERR: CreateConfigFile(): " + x.Message);
            }
        }

        private void MakePoster(string filename)
        {
            try
            {
                string poster = Path.Combine(Program.UpPath, timelapse.Code + ".jpg");
                File.Copy(filename, poster, true);
            }
            catch (Exception x)
            {
                Utils.TimelapseLog(timelapse, "ERR: MakePoster(" + filename + "): " + x);
            }
        }

        protected string CopyFfmpeg()
        {
            string PathDest = Path.Combine(Program.FfmpegCopyPath, "ffmpeg_" + timelapse.ID + ".exe");
            try
            {
                if (!File.Exists(PathDest))
                    File.Copy(Program.FfmpegExePath, PathDest, true);
            }
            catch (Exception x)
            {
                Utils.TimelapseLog(timelapse, "ERR: CopyFfmpeg(): " + x.Message);
                return "";
            }
            return PathDest;
        }

        protected void ExitProcess()
        {
            try
            {
                Environment.Exit(0);
                Utils.TimelapseLog(timelapse, "KillProcess on Exit: Timelapse ID: " + timelapse.ID);
            }
            catch (Exception x)
            {
                Utils.TimelapseLog(timelapse, "ERR: Recorder.ExitProcess: ", x);
                Console.WriteLine(DateTime.Now + " " + x.Message);
                Environment.Exit(0);
            }
        }
    }
}
