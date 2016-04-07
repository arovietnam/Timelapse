using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Diagnostics;
using EvercamMovieMaker;
using BLL.Dao;
using BLL.Entities;
using BLL.Common;
using EvercamV2;

namespace MovieCreator
{
  class Program
  {
      public static Evercam Evercam = new Evercam();

    static void Main(string[] args)
    {
        try
        {
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);
            Console.Title = "Evercam Movie Maker";
            Stopwatch sw = new Stopwatch();
            sw.Start();

            sw.Stop();
            //Console.WriteLine("Cache loaded in " + sw.ElapsedMilliseconds / 1000 + " s");
            Evercam.SANDBOX = Settings.EvercamSandboxMode;
            Evercam = new Evercam(Settings.EvercamClientID, Settings.EvercamClientSecret, Settings.EvercamClientUri);

            MovieMaker mm = new MovieMaker(delegate(string str) { Console.WriteLine(str); }, Evercam);
            mm.Start();
            
            Console.WriteLine("Press enter to continue...");
            Console.ReadLine();
        }
        catch (Exception ex)
        {
           //SaveMovieLog(DateTime.UtcNow + ": " + ex.Message);
        }
    }

    static void SaveMovieLog(string message)
    {
        StreamWriter stw = new StreamWriter("path\\MovieLog.txt", true);
        stw.WriteLine(message);
        stw.Close();
    }

    private static void CheckAndKillffmpeg1()
    {
        try
        {
            var list = Process.GetProcessesByName("ffmpeg");
            foreach (var pr in list)
            {
                try
                {
                    pr.Kill();
                    pr.WaitForExit();
                }
                catch (Exception)
                { }
            }
        }
        catch (Exception ex)
        {
        }
    }

    static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        Exception ex = e.ExceptionObject as Exception;
        Environment.ExitCode = 10;
    }
  }
}
