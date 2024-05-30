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
        private static string outputvideo, outputaudio, output, cpuorgpu, audiodelay;
        private static bool capturing;
        private static Process processcapture;
        private static ProcessStartInfo startinfocapture, startinfomerge;
        private static NAudio.Wave.MediaFoundationReader audioFileReader;
        private static NAudio.Wave.IWavePlayer waveOutDevice;
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
                    audioFileReader = new NAudio.Wave.MediaFoundationReader("1-hour-and-20-minutes-of-silence.mp3");
                    waveOutDevice = new NAudio.Wave.WaveOut();
                    waveOutDevice.Init(audioFileReader);
                    waveOutDevice.Play();
                    string localDate = DateTime.Now.ToString();
                    string name = localDate.Replace(" ", "-").Replace("/", "-").Replace(":", "-");
                    outputvideo = name + ".mkv";
                    outputaudio = name + ".wav";
                    output = name + ".mp4";
                    capturing = true;
                    Task.Run(() => StartCapture());
                }
                else
                {
                    if (wd[1] == 1 & capturing)
                    {
                        capturing = false;
                        Task.Run(() => StopCapture());
                    }
                }
                Thread.Sleep(70);
            }
        }
        private static void StartCapture()
        {
            startinfocapture = new ProcessStartInfo();
            startinfocapture.CreateNoWindow = false;
            startinfocapture.UseShellExecute = false;
            startinfocapture.RedirectStandardInput = true;
            startinfocapture.RedirectStandardOutput = true;
            startinfocapture.FileName = "ffmpeg.exe";
            if (cpuorgpu == "CPU")
                startinfocapture.Arguments = @"-filter_complex ddagrab=0,hwdownload,format=bgra -framerate 30 -offset_x 0 -offset_y 0 -video_size 1920x1080 -c:v libx264 -crf 20 -ss 10 " + outputvideo;
            if (cpuorgpu == "GPU")
                startinfocapture.Arguments = @"-init_hw_device d3d11va -filter_complex ddagrab=0 -framerate 30 -offset_x 0 -offset_y 0 -video_size 1920x1080 -c:v h264_nvenc -cq:v 20 -ss 10 " + outputvideo;
            CSCore.SoundIn.WasapiCapture capture = new CSCore.SoundIn.WasapiLoopbackCapture();
            capture.Initialize();
            CSCore.Codecs.WAV.WaveWriter wavewriter = new CSCore.Codecs.WAV.WaveWriter(outputaudio, capture.WaveFormat);
            capture.DataAvailable += (sound, card) =>
            {
                wavewriter.Write(card.Data, card.Offset, card.ByteCount);
            };
            Task.Run(() => processcapture = Process.Start(startinfocapture));
            Wait(10000 + Convert.ToInt32(audiodelay));
            capture.Start();
            for (int count = 0; count <= 60 * 60 * 1000; count++)
            {
                if (!capturing | count == 60 * 60 * 1000)
                {
                    capture.Stop();
                    wavewriter.Dispose();
                    break;
                }
                Thread.Sleep(1);
            }
        }
        private static void StopCapture()
        {
            processcapture.StandardInput.WriteLine('q');
            waveOutDevice.Stop();
            startinfomerge = new ProcessStartInfo();
            startinfomerge.CreateNoWindow = false;
            startinfomerge.UseShellExecute = false;
            startinfomerge.RedirectStandardInput = true;
            startinfomerge.RedirectStandardOutput = true;
            startinfomerge.FileName = "ffmpeg.exe";
            startinfomerge.Arguments = @"-i " + outputvideo + " -i " + outputaudio + " -c:v copy -c:a aac " + output;
            Thread.Sleep(20000);
            Process.Start(startinfomerge);
            Thread.Sleep(20000);
            File.Delete(outputvideo);
            File.Delete(outputaudio);
        }
        public static void Wait(int milliseconds)
        {
            var timer1 = new System.Windows.Forms.Timer();
            timer1.Interval = milliseconds;
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