using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using ValueStateChanged;
using Accord.Video.FFMPEG;
using System.Drawing;
using System.Timers;

namespace GameRecorder
{
    class Program
    {
        static void OnKeyDown(System.Windows.Forms.Keys keyData)
        {
            if (keyData == System.Windows.Forms.Keys.F1)
            {
                const string message = "• Author: Michaël André Franiatte.\n\r\n\r• Contact: michael.franiatte@gmail.com.\n\r\n\r• Publisher: https://github.com/michaelandrefraniatte.\n\r\n\r• Copyrights: All rights reserved, no permissions granted.\n\r\n\r• License: Not open source, not free of charge to use.";
                const string caption = "About";
                System.Windows.Forms.MessageBox.Show(message, caption, System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Information);
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
        public static extern bool GetAsyncKeyState(System.Windows.Forms.Keys vKey);
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
        private static Timer timer1;
        private static VideoFileWriter vf;
        private static Bitmap bp;
        private static Graphics gr;
        private static string outputvideo, outputaudio, output, outputvideotemp, outputaudiotemp, outputtemp, videodelay, ss;
        private static bool capturing;
        private static NAudio.Wave.MediaFoundationReader audioFileReader;
        private static NAudio.Wave.IWavePlayer waveOutDevice;
        private static CSCore.SoundIn.WasapiCapture captureaudio;
        private static CSCore.Codecs.WAV.WaveWriter wavewriter;
        private static StreamReader errorreadermerge;
        private static Process processmerge;
        public static System.Threading.ThreadStart threadstart;
        public static System.Threading.Thread thread;
        private static int width = 800, height = 600;
        public static uint CurrentResolution = 0;
        private static valuechanged ValueChanged = new valuechanged();
        static void Main()
        {
            TimeBeginPeriod(1);
            NtSetTimerResolution(1, true, ref CurrentResolution);
            handler = new ConsoleEventDelegate(ConsoleEventCallback);
            SetConsoleCtrlHandler(handler, true);
            if (!AlreadyRunning())
            {
                using (StreamReader createdfile = new StreamReader("params.txt"))
                {
                    createdfile.ReadLine();
                    videodelay = createdfile.ReadLine();
                }
                double ticks = double.Parse(videodelay);
                TimeSpan time = TimeSpan.FromMilliseconds(ticks);
                DateTime datetime = new DateTime(time.Ticks);
                ss = datetime.ToString("HH:mm:ss.fff");
                Task.Run(() => Start());
                if (!File.Exists("ffmpeg.exe"))
                {
                    System.Windows.Forms.MessageBox.Show("Not existing ffmpeg.exe! Please copy/paste ffmpeg.exe from the zip folder in this program folder, sorry closing.");
                }
                else
                {
                    MinimizeConsoleWindow();
                    Console.ReadLine();
                }
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
                ValueChanged[0] = GetAsyncKeyState(System.Windows.Forms.Keys.Decimal);
                ValueChanged[1] = GetAsyncKeyState(System.Windows.Forms.Keys.NumPad0);
                if (valuechanged._ValueChanged[0] & valuechanged._valuechanged[0] & !capturing)
                {
                    capturing = true;
                    string localDate = DateTime.Now.ToString();
                    string name = localDate.Replace(" ", "-").Replace("/", "-").Replace(":", "-");
                    outputvideo = name + ".avi";
                    outputaudio = name + ".wav";
                    output = name + ".mp4";
                    Task.Run(() => StartCapture());
                }
                else
                {
                    if (valuechanged._ValueChanged[1] & valuechanged._valuechanged[1] & capturing)
                    {
                        capturing = false;
                        Task.Run(() => StopCapture());
                    }
                }
                ValueChanged[2] = GetAsyncKeyState(System.Windows.Forms.Keys.F1);
                if (valuechanged._ValueChanged[2] & valuechanged._valuechanged[2])
                    OnKeyDown(System.Windows.Forms.Keys.F1);
                System.Threading.Thread.Sleep(70);
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
                captureaudio = new CSCore.SoundIn.WasapiLoopbackCapture();
                captureaudio.Initialize();
                wavewriter = new CSCore.Codecs.WAV.WaveWriter(outputaudio, captureaudio.WaveFormat);
                captureaudio.DataAvailable += (sound, card) =>
                {
                    wavewriter.Write(card.Data, card.Offset, card.ByteCount);
                };
                timer1 = new Timer(20);
                timer1.Enabled = true;
                timer1.Elapsed += new ElapsedEventHandler(timer_Elapsed);
                vf = new VideoFileWriter();
                vf.Open(outputvideo, width, height, 24, VideoCodec.MPEG4, 1000000);
                timer1.Start();
                captureaudio.Start();
            });
        }
        private static void timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            bp = new Bitmap(width, height);
            gr = Graphics.FromImage(bp);
            gr.CopyFromScreen(0, 0, 0, 0, new Size(width, height));
            bp.Save("test.jpg", System.Drawing.Imaging.ImageFormat.Jpeg);
            vf.WriteVideoFrame(bp);
        }
        private static void StopCapture()
        {
            outputaudiotemp = outputaudio;
            outputvideotemp = outputvideo;
            outputtemp = output;
            Task.Run(() => 
            {
                timer1.Stop();
                vf.Close();
            });
            Task.Run(() =>
            {
                captureaudio.Stop();
                wavewriter.Dispose();
                waveOutDevice.Stop();
            });
            System.Threading.Thread.Sleep(20000);
            processmerge = new Process();
            processmerge.StartInfo.CreateNoWindow = true;
            processmerge.StartInfo.UseShellExecute = false;
            processmerge.StartInfo.ErrorDialog = false;
            processmerge.StartInfo.RedirectStandardInput = true;
            processmerge.StartInfo.RedirectStandardError = true;
            processmerge.StartInfo.FileName = "ffmpeg.exe";
            processmerge.StartInfo.Arguments = @"-ss " + ss + " -i " + outputvideotemp + " -i " + outputaudiotemp + " -c:v copy -c:a aac -map 0:v:0 -map 1:a:0 " + outputtemp;
            processmerge.Start();
            errorreadermerge = processmerge.StandardError;
            string resultmerge = errorreadermerge.ReadToEnd();
            Console.WriteLine(resultmerge);
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
                threadstart = new System.Threading.ThreadStart(FormClose);
                thread = new System.Threading.Thread(threadstart);
                thread.Start();
                System.Threading.Thread.Sleep(2000);
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
namespace ValueStateChanged
{
    public class valuechanged
    {
        public static bool[] _valuechanged = { false, false, false };
        public static bool[] _ValueChanged = { false, false, false };
        public bool this[int index]
        {
            get { return _ValueChanged[index]; }
            set
            {
                if (_valuechanged[index] != value)
                    _ValueChanged[index] = true;
                else
                    _ValueChanged[index] = false;
                _valuechanged[index] = value;
            }
        }
    }
}