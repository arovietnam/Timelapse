using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.IO;
using EvercamV2;
using BLL.Common;

namespace TimelapseMaker
{
    class Tasks
    {
        private static Bitmap bmp;
        const string FfmpegExePath = "c:\\ffmpeg\\ffmpeg.exe";

        public static int ReindexImages(string imagesPath, int startIndex)
        {
            int count = 0;
            string reindexPath = imagesPath.Remove(imagesPath.LastIndexOf("images\\")) + "reindex\\";
            if (!Directory.Exists(reindexPath)) Directory.CreateDirectory(reindexPath);
            var images = new DirectoryInfo(imagesPath);
            foreach (FileInfo f in images.GetFiles())
            {
                string name = Path.GetFileNameWithoutExtension(f.FullName);
                int index = int.Parse(name) + startIndex;
                string r = reindexPath + index + Path.GetExtension(f.FullName);
                try
                {
                    f.CopyTo(r);
                    Console.WriteLine(count + "> " + r);
                    count++;
                }
                catch { Console.WriteLine("o> " + r); }
            }
            return count;
        }

        public static int MoveImages(string movePath, string toPath, int startIndex)
        {
            int count = 0;
            var move = new DirectoryInfo(movePath);
            foreach (FileInfo f in move.GetFiles())
            {
                string name = Path.GetFileNameWithoutExtension(f.FullName);
                int index = int.Parse(name);
                if (index >= startIndex)
                {
                    string m = toPath + f.Name;
                    try
                    {
                        f.MoveTo(m);
                        Console.WriteLine(count + ">> " + m);
                        count++;
                    }
                    catch { Console.WriteLine("o> " + m); }
                }
            }
            move.Delete();
            return count;
        }

        public static void CreateVideoFromImages(string filePath, int fps, string bitRate, string scale, string output)
        {
            RunProcess("-r " + fps + " -i " + filePath + @"\%00000d.jpg -c:v libx264 -r " + fps + " -profile:v main -preset slow -b:v " + bitRate + " -bufsize " + bitRate + " -vf scale=" + scale + " -pix_fmt yuv420p -y " + output);
        }

        public static void ConcatVideos(string baseMp4FileName, string tempVideoFileName, string mp4FileName)
        {
            try
            {
                string str = "files.txt";
                File.Delete(mp4FileName);
                File.Delete(tempVideoFileName);
                File.Delete(str);

                CreateConfigFile(baseMp4FileName, tempVideoFileName, str);

                RunProcess("-f concat -i " + str + " -c copy " + mp4FileName);
            }
            catch (Exception x)
            {
                Console.WriteLine(x.ToString());
            }
        }

        public static void WatermarkVideo(string input, string watermark, int position, string output)
        {
            string param = "";
            try
            {
                if (!File.Exists(watermark))
                {
                    // just copy input file as it is to output location
                    File.Copy(input, output, true);
                    return;
                }
                switch (position)
                {
                    case 1:
                        param = "-i " + input + " -i " + watermark + " -y -filter_complex \"overlay=10:10\" " + output;
                        break;
                    case 2:
                        param = "-i " + input + " -i " + watermark + " -y -filter_complex \"overlay=(main_w-overlay_w)-10:10\" " + output;
                        break;
                    case 3:
                        param = "-i " + input + " -i " + watermark + " -y -filter_complex \"overlay=(main_w-overlay_w)-10:(main_h-overlay_h)-10\" " + output;
                        break;
                    case 4:
                        param = "-i " + input + " -i " + watermark + " -y -filter_complex \"overlay=10:(main_h-overlay_h)-10\" " + output;
                        break;
                }
                RunProcess(param);
            }
            catch (Exception x)
            {
                Console.WriteLine(x.ToString());
            }
        }

        public static void CompressVideo(string input, string output, string bitRate, string scale)
        {
            string param = "-i " + input + " -y -vcodec libx264 -profile:v main -preset slow -b:v " + bitRate + " -bufsize " + bitRate + " -vf scale=" + scale + " " + output;
            RunProcess(param);
        }

        static void RunProcess(string parameters)
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
            //process.WaitForExit();
            string output = process.StandardError.ReadToEnd();
            Console.WriteLine(output);
        }

        protected static void CreateConfigFile(string mp4File, string newMp4File, string txtFileName)
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
                Console.WriteLine("ERR: CreateConfigFile(): " + x.Message);
            }
        }

        public static int DownloadImagesAtInterval(Evercam evercam, string user, Camera camera, DateTime fromDay, DateTime toDay, int fromHour, int toHour, int interval, int startIndex, string timestamp, string path)
        {
            int index = startIndex;
            DateTime from = new DateTime(fromDay.Year, fromDay.Month, fromDay.Day, fromHour, 0, 0);
            DateTime to = new DateTime(toDay.Year, toDay.Month, toDay.Day, toHour, 59, 59);
            while (from <= to)
            {
                byte[] data = null;
                long time = Utility.ToUnixTimestamp(Utils.ConvertToUtc(from, camera.Timezone));
                for (int i = 1; i <= 1; i++)
                {
                    try
                    {
                        int sec = interval * 60;
                        int range = Convert.ToInt32(sec / 2);
                        Snapshot snap = evercam.GetSnapshot(camera.ID, time.ToString(), true, range);
                        if (snap != null)
                        {
                            data = snap.ToBytes();

                            DateTime datetime = Utility.ToWindowsDateTime(snap.CreatedAt);
                            DateTime stamp = Utils.ConvertFromUtc(datetime, camera.Timezone);
                            //if (timestamp == "true")
                            //    Utils.TimestampImage(data, stamp.ToString(), 2);

                            Console.WriteLine(index + " : " + snap.CreatedAt + " = " + stamp);
                            break;
                        }
                    }
                    catch (Exception x)
                    {
                        //if (i < 2) Thread.Sleep(10 * 1000);
                        Console.WriteLine(" - ERR " + time + " :" + x.Message);
                    }
                }

                try
                {
                    if (SaveFile(path + index + ".jpg", data))
                        index++;
                }
                catch (Exception x)
                {
                    Console.WriteLine(" - ERR2 :" + x.Message);
                }

                from = from.AddMinutes(interval);
            }
            return index;
        }

        public static int DownloadAllImages(Evercam evercam, string user, string camera, string timezone, DateTime userFromDate, DateTime userToDate, int startIndex, string timestamp, string path)
        {
            byte[] data = null;
            int index = startIndex;
            long fromTimestamp = Utility.ToUnixTimestamp(userFromDate);
            long toTimestamp = Utility.ToUnixTimestamp(userToDate);
            List<Snapshot> snaps = evercam.GetSnapshots(camera, fromTimestamp, toTimestamp, 10000, null);

            foreach (Snapshot s in snaps)
            {
                try
                {
                    Snapshot snap = evercam.GetSnapshot(camera, s.CreatedAt.ToString(), true, 0);
                    DateTime datetime = Utility.ToWindowsDateTime(snap.CreatedAt);
                    DateTime snaptime = Utils.ConvertFromUtc(datetime, timezone);
                    if (snap != null && snaptime >= userFromDate)
                    {
                        data = snap.ToBytes();
                        
                        //if (timestamp == "true")
                        //    data = Utils.TimestampImage(data, snaptime.ToString(), Settings.WatermarkPostion);

                        Console.WriteLine(index + " : " + snap.CreatedAt + " = " + snaptime);

                        try
                        {
                            if (SaveFile(path + index + ".jpg", data))
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

        public static int DownloadTimelapseImages(Evercam evercam, string user, string camera, string timezone, DateTime userFromDate, DateTime userToDate, int startIndex, string path, int minutes)
        {
            List<Snapshot> snaps = new List<Snapshot>();
            byte[] data = null;
            int index = startIndex;
            DateTime newFromDate = userFromDate;
            DateTime newToDate = userToDate;

            while (newFromDate <= userToDate)
            {
                newToDate = newFromDate.AddMinutes(minutes);
                long fromTimestamp = Utility.ToUnixTimestamp(newFromDate);
                long toTimestamp = Utility.ToUnixTimestamp(newToDate);

                try
                {
                    snaps = evercam.GetSnapshots(camera, fromTimestamp, toTimestamp, 1, null);
                    if (snaps.Count > 0)
                    {
                        Snapshot snap = evercam.GetSnapshot(camera, snaps[0].CreatedAt.ToString(), true, 0);
                        if (snap != null)
                        {
                            data = snap.ToBytes();

                            DateTime datetime = Utility.ToWindowsDateTime(snap.CreatedAt);
                            DateTime snaptime = Utils.ConvertFromUtc(datetime, timezone);
                            Console.WriteLine(index + " : " + snap.CreatedAt + " = " + snaptime);

                            try
                            {
                                if (SaveFile(path + index + ".jpg", data))
                                    index++;
                            }
                            catch (Exception x)
                            {
                                Console.WriteLine(" - ERR1 :" + x.Message);
                            }
                        }
                    }
                }
                catch (Exception x) { }

                newFromDate = newToDate;
            }
            return index;
        }

        public static void ResizeImages(string imagesPath, string savePath, int x, int y, int w, int h)
        {
            if (!Directory.Exists(savePath))
                Directory.CreateDirectory(savePath);

            string[] images = Directory.GetFiles(imagesPath, "*.jpg", SearchOption.AllDirectories);
            foreach(string s in images)
            {
                ResizeSave(s, Path.Combine(savePath, new FileInfo(s).Name), x, y, w, h);
            }
        }

        public static void ResizeSave(string imageFile, string saveFile, int x, int y, int w, int h)
        {
            ImageCodecInfo jpgEncoder = GetEncoder(ImageFormat.Jpeg);
            System.Drawing.Imaging.Encoder myEncoder = System.Drawing.Imaging.Encoder.Quality;
            EncoderParameter myEncoderParameter = new EncoderParameter(myEncoder, 50L);
            EncoderParameters myEncoderParameters = new EncoderParameters(1);
            myEncoderParameters.Param[0] = myEncoderParameter;

            using (var streamOriginal = new MemoryStream())
            using (FileStream file = new FileStream(imageFile, FileMode.Open, FileAccess.Read))
            {
                byte[] bytes = new byte[file.Length];
                file.Read(bytes, 0, (int)file.Length);
                streamOriginal.Write(bytes, 0, (int)file.Length);
                Console.WriteLine(" - " + saveFile);

                using (var imgOriginal = Image.FromStream(streamOriginal))
                using (var resizedBmp = new Bitmap(imgOriginal.Width, imgOriginal.Height))
                {
                    if (imgOriginal.Width == w && imgOriginal.Height == h) {
                        imgOriginal.Save(saveFile, jpgEncoder, myEncoderParameters);
                        Console.WriteLine(" --- skipped");
                    }
                    else
                    {
                        using (var graphics = Graphics.FromImage((Image)resizedBmp))
                        {
                            graphics.InterpolationMode = InterpolationMode.Default;
                            graphics.DrawImage(imgOriginal, 0, 0, imgOriginal.Width, imgOriginal.Height);
                        }

                        // center the cropping area if x, y are not provided
                        if (x < 0) x = (imgOriginal.Width - w) / 2;
                        if (y < 0) y = (imgOriginal.Height - h) / 2;

                        //create the cropping rectangle
                        var rectangle = new Rectangle(x, y, w, h);

                        //crop
                        using (var croppedBmp = resizedBmp.Clone(rectangle, resizedBmp.PixelFormat))
                        {
                            croppedBmp.Save(saveFile, jpgEncoder, myEncoderParameters);
                        }
                    }
                }
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

        private static ImageCodecInfo GetEncoder(ImageFormat format)
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageDecoders();
            foreach (ImageCodecInfo codec in codecs)
            {
                if (codec.FormatID == format.Guid)
                {
                    return codec;
                }
            }
            return null;
        }

        private static Image ResizeImage(Image img, int x, int y, int w, int h)
        {
            if (img.Width == w && img.Height == h)
            {
                Console.Write(" - skipped");
                return img;
            }

            bmp = new Bitmap(w, h);
            using (var graphics = Graphics.FromImage(bmp))
            {
                graphics.InterpolationMode = InterpolationMode.Low;
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.PixelOffsetMode = PixelOffsetMode.HighSpeed;
                using (var wrapMode = new ImageAttributes())
                {
                    wrapMode.SetWrapMode(WrapMode.TileFlipXY);
                    var destRect = new Rectangle(0, 0, w, h);
                    graphics.DrawImage(img, destRect, x, y, w, h, GraphicsUnit.Pixel, wrapMode);
                }
            }
            return bmp;
        }
    }
}
