using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using ValueStateChanged;
using System.IO;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Extras;

namespace RecordYoutube
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }
        [DllImport("user32.dll")]
        public static extern bool GetAsyncKeyState(System.Windows.Forms.Keys vKey);
        [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
        public static extern uint TimeBeginPeriod(uint ms);
        [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
        public static extern uint TimeEndPeriod(uint ms);
        [DllImport("ntdll.dll", EntryPoint = "NtSetTimerResolution")]
        public static extern void NtSetTimerResolution(uint DesiredResolution, bool SetResolution, ref uint CurrentResolution);
        private static uint CurrentResolution = 0;
        private string outputvideo, outputaudio, output, outputvideotemp, outputaudiotemp, outputtemp, cpuorgpu, commandcpu, commandgpu, videodelay, ss;
        private bool capturing;
        private WasapiOut wasapiOut;
        private WasapiLoopbackCapture capture;
        private WaveFileWriter writer;
        private Process processcapturevideo, processmerge;
        private valuechanged ValueChanged = new valuechanged();
        private void Form1_Shown(object sender, EventArgs e)
        {
            TimeBeginPeriod(1);
            NtSetTimerResolution(1, true, ref CurrentResolution);
            if (!File.Exists(Application.StartupPath + @"\ffmpeg.exe"))
            {
                MessageBox.Show("Not existing ffmpeg.exe! Please copy/paste ffmpeg.exe from the zip folder in this program folder, sorry closing.");
                this.Close();
            }
            else
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
                    videodelay = createdfile.ReadLine();
                }
                double ticks = double.Parse(videodelay);
                TimeSpan time = TimeSpan.FromMilliseconds(ticks);
                DateTime datetime = new DateTime(time.Ticks);
                ss = datetime.ToString("HH:mm:ss.fff");
            }
        }
        private void timer1_Tick(object sender, EventArgs e)
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
        }
        private void StartCapture()
        {
            Task.Run(() =>
            {
                capture = new NAudio.Wave.WasapiLoopbackCapture();
                writer = new WaveFileWriter(outputaudio, capture.WaveFormat);
                capture.DataAvailable += (s, a) =>
                {
                    writer.Write(a.Buffer, 0, a.BytesRecorded);
                    writer.Flush();
                };
                capture.RecordingStopped += (s, a) =>
                {
                    writer.Dispose();
                    writer = null;
                    capture.Dispose();
                    wasapiOut.Stop();
                };
                var device = new MMDeviceEnumerator().GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                var silenceProvider = new SilenceProvider(capture.WaveFormat);
                wasapiOut = new WasapiOut(device, AudioClientShareMode.Shared, false, 250);
                wasapiOut.Init(silenceProvider);
                wasapiOut.Play();
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
        private void ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data.IndexOf("frame= ") != -1 & capturing)
            {
                capture.StartRecording();
                processcapturevideo.CancelErrorRead();
            }
        }
        private void StopCapture()
        {
            outputaudiotemp = outputaudio;
            outputvideotemp = outputvideo;
            outputtemp = output;
            Task.Run(() => processcapturevideo.StandardInput.WriteLine('q'));
            Task.Run(() => capture.StopRecording());
            Thread.Sleep(4000);
            do
            {
                Thread.Sleep(1000);

            } while (new FileInfo(outputaudiotemp).Length <= 0 | new FileInfo(outputvideotemp).Length <= 0);
            processmerge = new Process();
            processmerge.StartInfo.CreateNoWindow = true;
            processmerge.StartInfo.UseShellExecute = false;
            processmerge.StartInfo.ErrorDialog = false;
            processmerge.StartInfo.RedirectStandardInput = false;
            processmerge.StartInfo.RedirectStandardError = false;
            processmerge.StartInfo.FileName = "ffmpeg.exe";
            processmerge.StartInfo.Arguments = @"-ss " + ss + " -i " + outputvideotemp + " -i " + outputaudiotemp + " -c:v copy -c:a aac -map 0:v:0 -map 1:a:0 " + outputtemp;
            processmerge.Start();
            Thread.Sleep(4000);
            do
            {
                Thread.Sleep(1000);
                if (File.Exists(outputaudiotemp))
                    try
                    {
                        File.Delete(outputaudiotemp);
                    }
                    catch { }
                if (File.Exists(outputvideotemp))
                    try
                    {
                        File.Delete(outputvideotemp);
                    }
                    catch { }

            } while (File.Exists(outputaudiotemp) | File.Exists(outputvideotemp));
        }
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (capturing)
            {
                capturing = false;
                StopCapture();
            }
        }
        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            OnKeyDown(e.KeyData);
        }
        private static void OnKeyDown(System.Windows.Forms.Keys keyData)
        {
            if (keyData == System.Windows.Forms.Keys.F1)
            {
                const string message = "• Author: Michaël André Franiatte.\n\r\n\r• Contact: michael.franiatte@gmail.com.\n\r\n\r• Publisher: https://github.com/michaelandrefraniatte.\n\r\n\r• Copyrights: All rights reserved, no permissions granted.\n\r\n\r• License: Not open source, not free of charge to use.";
                const string caption = "About";
                System.Windows.Forms.MessageBox.Show(message, caption, System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Information);
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