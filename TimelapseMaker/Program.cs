using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Configuration;
using System.IO;
using EvercamV2;

namespace TimelapseMaker
{
    class Program
    {
        static void Main(string[] args)
        {
            string ApiId = ConfigurationSettings.AppSettings["EvercamApiId"];
            string ApiKey = ConfigurationSettings.AppSettings["EvercamApiKey"];
            string UserId = ConfigurationSettings.AppSettings["UserId"];
            string CameraId = ConfigurationSettings.AppSettings["CameraId"];
            DateTime fromDay = DateTime.Parse(ConfigurationSettings.AppSettings["FromDate"]);
            DateTime toDay = DateTime.Parse(ConfigurationSettings.AppSettings["ToDate"]);
            int fromHour = int.Parse(ConfigurationSettings.AppSettings["FromHour"]);
            int toHour = int.Parse(ConfigurationSettings.AppSettings["ToHour"]);
            int fromMin = int.Parse(ConfigurationSettings.AppSettings["FromMin"]);
            int toMin = int.Parse(ConfigurationSettings.AppSettings["ToMin"]);
            int fromSec = int.Parse(ConfigurationSettings.AppSettings["FromSec"]);
            int toSec = int.Parse(ConfigurationSettings.AppSettings["ToSec"]);
            int intervalMin = int.Parse(ConfigurationSettings.AppSettings["IntervalMin"]);
            int startIndex = int.Parse(ConfigurationSettings.AppSettings["StartIndex"]);
            int moveIndex = int.Parse(ConfigurationSettings.AppSettings["MoveIndex"]);
            int fps = int.Parse(ConfigurationSettings.AppSettings["Fps"]);
            int x = int.Parse(ConfigurationSettings.AppSettings["CropX"]);
            int y = int.Parse(ConfigurationSettings.AppSettings["CropY"]);
            int w = int.Parse(ConfigurationSettings.AppSettings["CropW"]);
            int h = int.Parse(ConfigurationSettings.AppSettings["CropH"]);
            int watermarkPostion = int.Parse(ConfigurationSettings.AppSettings["WatermarkPostion"]);
            int timelapseId = int.Parse(ConfigurationSettings.AppSettings["TimelapseId"]);
            string imagesDir = ConfigurationSettings.AppSettings["ImagesDir"];
            string resizedDir = ConfigurationSettings.AppSettings["ResizedDir"];
            string moveDir = ConfigurationSettings.AppSettings["MoveDir"];
            string toDir = ConfigurationSettings.AppSettings["ToDir"];
            string videoDir = ConfigurationSettings.AppSettings["VideoDir"];
            string videoFile = ConfigurationSettings.AppSettings["VideoFile"];
            string concatFileA = ConfigurationSettings.AppSettings["ConcatFileA"];
            string concatFileB = ConfigurationSettings.AppSettings["ConcatFileB"];
            string watermarkFile = ConfigurationSettings.AppSettings["WatermarkFile"];
            string bitRate = ConfigurationSettings.AppSettings["BitRate"];
            string scale = ConfigurationSettings.AppSettings["Scale"];
            string reindexImages = ConfigurationSettings.AppSettings["ReindexImages"].ToLower();
            string moveImages = ConfigurationSettings.AppSettings["MoveImages"].ToLower();
            string downloadImages = ConfigurationSettings.AppSettings["DownloadImages"].ToLower();
            string resizeImages = ConfigurationSettings.AppSettings["ResizeImages"].ToLower();
            string makeVideo = ConfigurationSettings.AppSettings["MakeVideo"].ToLower();
            string timestampVideo = ConfigurationSettings.AppSettings["TimestampVideo"].ToLower();
            string watermarkVideo = ConfigurationSettings.AppSettings["WatermarkVideo"].ToLower();
            string compressVideo = ConfigurationSettings.AppSettings["CompressVideo"].ToLower();
            string concatVideos = ConfigurationSettings.AppSettings["ConcatVideos"].ToLower();

            Evercam evercam = new Evercam(ApiId, ApiKey);
            Camera camera = evercam.GetCamera(CameraId);
            DateTime userFrom = new DateTime(fromDay.Year, fromDay.Month, fromDay.Day, fromHour, fromMin, fromSec);
            DateTime userTo = new DateTime(toDay.Year, toDay.Month, toDay.Day, toHour, toMin, toSec);
            
            if (reindexImages == "true")
            {
                string s = DateTime.UtcNow.ToString();
                int count = Tasks.ReindexImages(imagesDir, startIndex);
                BLL.Common.Utils.SendMail("Timelapse Images (" + CameraId + ":" + timelapseId + ")", "Reindexed: " + count + Environment.NewLine + "Started: " + s + Environment.NewLine + "Finished: " + DateTime.UtcNow, BLL.Common.Settings.DebugEmail);
            }

            if (moveImages == "true")
            {
                string s = DateTime.UtcNow.ToString();
                int count = Tasks.MoveImages(moveDir, toDir, moveIndex);
                BLL.Common.Utils.SendMail("Timelapse Images (" + CameraId + ":" + timelapseId + ")", "Moved: " + count + Environment.NewLine + "Started: " + s + Environment.NewLine + "Finished: " + DateTime.UtcNow, BLL.Common.Settings.DebugEmail);
            }

            if (concatVideos == "true")
            {
                Tasks.ConcatVideos(concatFileA, concatFileB, videoFile);
            }

            if (downloadImages == "true" && !string.IsNullOrEmpty(imagesDir))
            {
                if (intervalMin > 0)
                {
                    if (timelapseId > 0)
                    {
                        Tasks.DownloadTimelapseImages(
                            evercam,
                            UserId,
                            camera.ID,
                            Utility.ToWindowsTimezone(camera.Timezone),
                            userFrom,
                            userTo,
                            startIndex,
                            imagesDir,
                            intervalMin);
                    }
                    else
                    {
                        Tasks.DownloadImagesAtInterval(
                            evercam,
                            UserId,
                            camera,
                            fromDay,
                            toDay,
                            fromHour,
                            toHour,
                            intervalMin,
                            startIndex,
                            timestampVideo,
                            imagesDir);
                    }
                }
                else
                {
                    Tasks.DownloadAllImages(
                        evercam,
                        UserId,
                        camera.ID,
                        Utility.ToWindowsTimezone(camera.Timezone),
                        userFrom,
                        userTo,
                        startIndex,
                        timestampVideo,
                        imagesDir);
                }
            }

            if (resizeImages == "true" && !string.IsNullOrEmpty(resizedDir))
            {
                Tasks.ResizeImages(imagesDir, resizedDir, x, y, w, h);
            }

            if (makeVideo == "true" && !string.IsNullOrEmpty(videoDir))
            {
                Tasks.CreateVideoFromImages(imagesDir, fps, bitRate, scale, videoDir + "base-" + videoFile);
            }

            if (watermarkVideo == "true" && !string.IsNullOrEmpty(watermarkFile))
            {
                Tasks.WatermarkVideo(videoDir + "base-" + videoFile, watermarkFile, watermarkPostion, videoDir + videoFile);
            }

            if (compressVideo == "true" && !string.IsNullOrEmpty(videoDir))
            {
                Tasks.CompressVideo(videoDir + videoFile, videoDir + "compressed-" + videoFile, bitRate, scale);
            }

            Console.WriteLine("Press any key to exit...");
            Console.ReadLine();
        }
    }
}
