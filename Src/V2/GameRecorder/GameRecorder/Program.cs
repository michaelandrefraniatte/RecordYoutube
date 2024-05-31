using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Threading;
using System.Diagnostics;
using System.IO;
using ValueStateChanged;

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
        private static string outputvideo, outputaudio, output, outputvideotemp, outputaudiotemp, outputtemp, cpuorgpu, commandcpu, commandgpu;
        private static bool capturing;
        private static NAudio.Wave.MediaFoundationReader audioFileReader;
        private static NAudio.Wave.IWavePlayer waveOutDevice;
        private static CSCore.SoundIn.WasapiCapture captureaudio;
        private static CSCore.Codecs.WAV.WaveWriter wavewriter;
        private static StreamReader errorreadermerge;
        private static Process processcapturevideo, processmerge;
        public static ThreadStart threadstart;
        public static Thread thread;
        public static uint CurrentResolution = 0;
        private static valuechanged ValueChanged = new valuechanged();
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
                ValueChanged[0] = GetAsyncKeyState(Keys.Decimal);
                ValueChanged[1] = GetAsyncKeyState(Keys.NumPad0);
                if (valuechanged._ValueChanged[0] & valuechanged._valuechanged[0] & !capturing)
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
                    if (valuechanged._ValueChanged[1] & valuechanged._valuechanged[1] & capturing)
                    {
                        capturing = false;
                        Task.Run(() => StopCapture());
                    }
                }
                ValueChanged[2] = GetAsyncKeyState(Keys.F1);
                if (valuechanged._ValueChanged[2] & valuechanged._valuechanged[2])
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
                captureaudio = new CSCore.SoundIn.WasapiLoopbackCapture();
                captureaudio.Initialize();
                wavewriter = new CSCore.Codecs.WAV.WaveWriter(outputaudio, captureaudio.WaveFormat);
                captureaudio.DataAvailable += (sound, card) =>
                {
                    wavewriter.Write(card.Data, card.Offset, card.ByteCount);
                };
                processcapturevideo = new Process();
                processcapturevideo.StartInfo.CreateNoWindow = true;
                processcapturevideo.StartInfo.UseShellExecute = false;
                processcapturevideo.StartInfo.ErrorDialog = false;
                processcapturevideo.StartInfo.RedirectStandardInput = true;
                processcapturevideo.StartInfo.RedirectStandardError = true;
                processcapturevideo.StartInfo.FileName = "ffmpeg.exe";
                object[] args = new object[] { outputvideo };
                if (cpuorgpu == "CPU")
                    processcapturevideo.StartInfo.Arguments = String.Format(commandcpu, args);
                if (cpuorgpu == "GPU")
                    processcapturevideo.StartInfo.Arguments = String.Format(commandgpu, args);
                processcapturevideo.ErrorDataReceived += ErrorDataReceived;
                processcapturevideo.EnableRaisingEvents = true;
                processcapturevideo.Start();
                processcapturevideo.BeginErrorReadLine();
            });
        }
        private static void ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data.IndexOf("frame= ") != -1 & capturing)
            {
                captureaudio.Start();
            }
            Console.WriteLine(e.Data + Environment.NewLine);
            if (e.Data.IndexOf("frame= ") != -1)
                processcapturevideo.CancelErrorRead();
        }
        private static void StopCapture()
        {
            outputaudiotemp = outputaudio;
            outputvideotemp = outputvideo;
            outputtemp = output;
            Task.Run(() => processcapturevideo.StandardInput.WriteLine('q'));
            Task.Run(() =>
            {
                captureaudio.Stop();
                wavewriter.Dispose();
                waveOutDevice.Stop();
            });
            Thread.Sleep(10000);
            processmerge = new Process();
            processmerge.StartInfo.CreateNoWindow = true;
            processmerge.StartInfo.UseShellExecute = false;
            processmerge.StartInfo.ErrorDialog = false;
            processmerge.StartInfo.RedirectStandardInput = true;
            processmerge.StartInfo.RedirectStandardError = true;
            processmerge.StartInfo.FileName = "ffmpeg.exe";
            processmerge.StartInfo.Arguments = @"-i " + outputvideotemp + " -i " + outputaudiotemp + " -c copy " + outputtemp;
            processmerge.Start();
            errorreadermerge = processmerge.StandardError;
            string resultmerge = errorreadermerge.ReadToEnd();
            Console.WriteLine(resultmerge);
            Thread.Sleep(20000);
            File.Delete(outputvideotemp);
            File.Delete(outputaudiotemp);
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