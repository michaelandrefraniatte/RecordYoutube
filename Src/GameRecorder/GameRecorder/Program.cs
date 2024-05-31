using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Threading;
using System.Diagnostics;
using System.IO;

namespace GameRecorder
{
    class Program
    {
        static void OnKeyDown(Keys keyData)
        {
            if (keyData == Keys.F1)
            {
                const string message = "• Author: Michaël André Franiatte.\n\r\n\r• Contact: michael.franiatte@gmail.com.\n\r\n\r• Publisher: https://github.com/michaelandrefraniatte.\n\r\n\r• Copyrights: All rights reserved, no permissions granted.\n\r\n\r• License: Not open source, not free of charge to use.";
                const string caption = "About";
                MessageBox.Show(message, caption, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        [DllImport("advapi32.dll")]
        private static extern bool LogonUser(string lpszUsername, string lpszDomain, string lpszPassword, int dwLogonType, int dwLogonProvider, out IntPtr phToken);
        [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
        public static extern uint TimeBeginPeriod(uint ms);
        [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
        public static extern uint TimeEndPeriod(uint ms);
        [DllImport("ntdll.dll", EntryPoint = "NtSetTimerResolution")]
        public static extern void NtSetTimerResolution(uint DesiredResolution, bool SetResolution, ref uint CurrentResolution);
        [DllImport("user32.dll")]
        public static extern bool GetAsyncKeyState(Keys vKey);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleCtrlHandler(ConsoleEventDelegate callback, bool add);
        [DllImport("Kernel32.dll", CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        private static extern IntPtr GetConsoleWindow();
        [DllImport("User32.dll", CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ShowWindow([In] IntPtr hWnd, [In] Int32 nCmdShow);
        const Int32 SW_MINIMIZE = 6;
        static ConsoleEventDelegate handler;
        private delegate bool ConsoleEventDelegate(int eventType);
        private static string outputvideo, outputaudio, output, outputvideotemp, outputaudiotemp, outputtemp, cpuorgpu, audiodelay, commandcpu, commandgpu;
        private static bool capturing;
        private static Process processcapture;
        private static ProcessStartInfo startinfocapturevideo, startinfomerge;
        private static NAudio.Wave.MediaFoundationReader audioFileReader;
        private static NAudio.Wave.IWavePlayer waveOutDevice;
        private static CSCore.SoundIn.WasapiCapture captureaudio;
        private static CSCore.Codecs.WAV.WaveWriter wavewriter;
        public static ThreadStart threadstart;
        public static Thread thread;
        public static uint CurrentResolution = 0;
        public static int[] wd = { 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2 };
        public static int[] wu = { 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2 };
        public static bool[] ws = { false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false };
        static void valchanged(int n, bool val)
        {
            if (val)
            {
                if (wd[n] <= 1)
                {
                    wd[n] = wd[n] + 1;
                }
                wu[n] = 0;
            }
            else
            {
                if (wu[n] <= 1)
                {
                    wu[n] = wu[n] + 1;
                }
                wd[n] = 0;
            }
            ws[n] = val;
        }
        static void Main()
        {
            TimeBeginPeriod(1);
            NtSetTimerResolution(1, true, ref CurrentResolution);
            MinimizeConsoleWindow();
            handler = new ConsoleEventDelegate(ConsoleEventCallback);
            SetConsoleCtrlHandler(handler, true);
            if (!AlreadyRunning())
            {
                using (StreamReader createdfile = new StreamReader("params.txt"))
                {
                    createdfile.ReadLine();
                    cpuorgpu = createdfile.ReadLine();
                    createdfile.ReadLine();
                    commandcpu = createdfile.ReadLine();
                    createdfile.ReadLine();
                    commandgpu = createdfile.ReadLine();
                    createdfile.ReadLine();
                    audiodelay = createdfile.ReadLine();
                }
                Task.Run(() => Start());
                Console.ReadLine();
            }
        }
        public static bool AlreadyRunning()
        {
            Process[] processes = Process.GetProcessesByName("GameRecorder");
            if (processes.Length > 1)
                return true;
            else
                return false;
        }
        public static void Start()
        {
            for (; ; )
            {
                valchanged(0, GetAsyncKeyState(Keys.Decimal));
                valchanged(1, GetAsyncKeyState(Keys.NumPad0));
                if (wd[0] == 1 & !capturing)
                {
                    capturing = true;
                    string localDate = DateTime.Now.ToString();
                    string name = localDate.Replace(" ", "-").Replace("/", "-").Replace(":", "-");
                    outputvideo = name + ".mkv";
                    outputaudio = name + ".wav";
                    output = name + ".mp4";
                    Task.Run(() => StartCapture());
                }
                else
                {
                    if (wd[1] == 1 & capturing)
                    {
                        capturing = false;
                        StopCapture();
                    }
                }
                valchanged(2, GetAsyncKeyState(Keys.F1));
                if (wd[2] == 1)
                    OnKeyDown(Keys.F1);
                Thread.Sleep(70);
            }
        }
        private static void StartCapture()
        {
            audioFileReader = new NAudio.Wave.MediaFoundationReader("1-hour-and-20-minutes-of-silence.mp3");
            waveOutDevice = new NAudio.Wave.WaveOut();
            waveOutDevice.Init(audioFileReader);
            waveOutDevice.Play();
            Task.Run(() =>
            {
                startinfocapturevideo = new ProcessStartInfo();
                startinfocapturevideo.CreateNoWindow = false;
                startinfocapturevideo.UseShellExecute = false;
                startinfocapturevideo.RedirectStandardInput = true;
                startinfocapturevideo.RedirectStandardOutput = true;
                startinfocapturevideo.FileName = "ffmpeg.exe";
                object[] args = new object[] { outputvideo };
                if (cpuorgpu == "CPU")
                    startinfocapturevideo.Arguments = String.Format(commandcpu, args);
                if (cpuorgpu == "GPU")
                    startinfocapturevideo.Arguments = String.Format(commandgpu, args);
                processcapture = Process.Start(startinfocapturevideo);
            });
            Task.Run(() =>
            {
                captureaudio = new CSCore.SoundIn.WasapiLoopbackCapture();
                captureaudio.Initialize();
                wavewriter = new CSCore.Codecs.WAV.WaveWriter(outputaudio, captureaudio.WaveFormat);
                captureaudio.DataAvailable += (sound, card) =>
                {
                    wavewriter.Write(card.Data, card.Offset, card.ByteCount);
                };
                captureaudio.Start();
            });
        }
        private static void StopCapture()
        {
            Task.Run(() => processcapture.StandardInput.WriteLine('q'));
            Wait(audiodelay);
            Task.Run(() =>
            {
                captureaudio.Stop();
                wavewriter.Dispose();
                waveOutDevice.Stop();
            });
            StreamReader errorreaderaudio;
            Process processdurationaudio = new Process();
            processdurationaudio.StartInfo.UseShellExecute = false;
            processdurationaudio.StartInfo.ErrorDialog = false;
            processdurationaudio.StartInfo.RedirectStandardError = true;
            processdurationaudio.StartInfo.FileName = "ffmpeg.exe";
            processdurationaudio.StartInfo.Arguments = "-i " + outputaudio;
            StreamReader errorreadervideo;
            Process processdurationvideo = new Process();
            processdurationvideo.StartInfo.UseShellExecute = false;
            processdurationvideo.StartInfo.ErrorDialog = false;
            processdurationvideo.StartInfo.RedirectStandardError = true;
            processdurationvideo.StartInfo.FileName = "ffmpeg.exe";
            processdurationvideo.StartInfo.Arguments = "-i " + outputvideo;
            outputaudiotemp = outputaudio;
            outputvideotemp = outputvideo;
            outputtemp = output;
            Thread.Sleep(20000);
            processdurationaudio.Start();
            errorreaderaudio = processdurationaudio.StandardError;
            processdurationaudio.WaitForExit();
            string resultaudio = errorreaderaudio.ReadToEnd();
            string durationaudio = resultaudio.Substring(resultaudio.IndexOf("Duration: ") + "Duration: ".Length, "00:00:00.00".Length);
            processdurationvideo.Start();
            errorreadervideo = processdurationvideo.StandardError;
            processdurationvideo.WaitForExit();
            string resultvideo = errorreadervideo.ReadToEnd();
            string durationvideo = resultvideo.Substring(resultvideo.IndexOf("Duration: ") + "Duration: ".Length, "00:00:00.00".Length);
            TimeSpan duration = DateTime.Parse(durationaudio).Subtract(DateTime.Parse(durationvideo));
            string difference = duration.ToString();
            if (difference.StartsWith("-"))
            {
                string soonervideo = difference.ToString().Substring(1, "00:00:00.00".Length);
                startinfomerge = new ProcessStartInfo();
                startinfomerge.CreateNoWindow = false;
                startinfomerge.UseShellExecute = false;
                startinfomerge.RedirectStandardInput = true;
                startinfomerge.RedirectStandardOutput = true;
                startinfomerge.FileName = "ffmpeg.exe";
                startinfomerge.Arguments = @"-ss " + soonervideo + "0 -to " + durationvideo + "0 -i " + outputvideotemp + " -ss 00:00:00.000 -to " + durationaudio + "0 -i " + outputaudiotemp + " -map 0:v:0 -map 1:a:0 -y " + outputtemp;
            }
            else
            {
                string sooneraudio = difference.ToString().Substring(0, "00:00:00.00".Length);
                startinfomerge = new ProcessStartInfo();
                startinfomerge.CreateNoWindow = false;
                startinfomerge.UseShellExecute = false;
                startinfomerge.RedirectStandardInput = true;
                startinfomerge.RedirectStandardOutput = true;
                startinfomerge.FileName = "ffmpeg.exe";
                startinfomerge.Arguments = @"-ss 00:00:00.000 -to " + durationvideo + "0 -i " + outputvideotemp + " -ss " + sooneraudio + "0 -to " + durationaudio + "0 -i " + outputaudiotemp + " -map 0:v:0 -map 1:a:0 -y " + outputtemp;
            }
            Process.Start(startinfomerge);
            Thread.Sleep(20000);
            File.Delete(outputvideotemp);
            File.Delete(outputaudiotemp);
        }
        public static void Wait(string delay)
        {
            if (delay != "0")
            {
                var timer1 = new System.Windows.Forms.Timer();
                timer1.Interval = Convert.ToInt32(delay);
                timer1.Enabled = true;
                timer1.Start();
                timer1.Tick += (s, e) =>
                {
                    timer1.Enabled = false;
                    timer1.Stop();
                };
                while (timer1.Enabled)
                {
                    Application.DoEvents();
                    Thread.Sleep(1);
                }
            }
        }
        private static void MinimizeConsoleWindow()
        {
            IntPtr hWndConsole = GetConsoleWindow();
            ShowWindow(hWndConsole, SW_MINIMIZE);
        }
        static bool ConsoleEventCallback(int eventType)
        {
            if (eventType == 2)
            {
                threadstart = new ThreadStart(FormClose);
                thread = new Thread(threadstart);
                thread.Start();
                Thread.Sleep(2000);
            }
            return false;
        }
        private static void FormClose()
        {
            if (capturing)
            {
                capturing = false;
                StopCapture();
            }
        }
    }
}