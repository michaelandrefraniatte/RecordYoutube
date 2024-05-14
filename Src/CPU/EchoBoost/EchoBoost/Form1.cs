using System;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Channels;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
namespace EchoBoost
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }
        [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
        private static extern uint TimeBeginPeriod(uint ms);
        [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
        private static extern uint TimeEndPeriod(uint ms);
        [DllImport("ntdll.dll", EntryPoint = "NtSetTimerResolution")]
        private static extern void NtSetTimerResolution(uint DesiredResolution, bool SetResolution, ref uint CurrentResolution);
        private static uint CurrentResolution = 0;
        private static bool closeonicon = false;
        private WasapiLoopbackCapture waveIn = null;
        private BufferedWaveProvider waveProvider = null;
        private WasapiOut waveOut = null;
        private static bool capturedevicefirst = false;
        private bool ison = false, closed = false;
        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            OnKeyDown(e.KeyData);
        }
        private void OnKeyDown(Keys keyData)
        {
            if (keyData == Keys.F1)
            {
                const string message = "• Author: Michaël André Franiatte.\n\r\n\r• Contact: michael.franiatte@gmail.com.\n\r\n\r• Publisher: https://github.com/michaelandrefraniatte.\n\r\n\r• Copyrights: All rights reserved, no permissions granted.\n\r\n\r• License: Not open source, not free of charge to use.";
                const string caption = "About";
                MessageBox.Show(message, caption, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            if (keyData == Keys.Escape)
            {
                this.Close();
            }
        }
        private void Form1_Load(object sender, EventArgs e)
        {
            TimeBeginPeriod(1);
            NtSetTimerResolution(1, true, ref CurrentResolution);
            TrayMenuContext();
            MinimzedTray();
            int inc = 0;
            Task.Run(() => {
                do
                {
                    try
                    {
                        var enumerator = new MMDeviceEnumerator();
                        MMDevice wasapi = null;
                        foreach (var mmdevice in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
                        {
                            wasapi = mmdevice;
                            if (!capturedevicefirst)
                                break;
                        }
                        waveIn = new WasapiLoopbackCapture();
                        waveOut = new WasapiOut(wasapi, AudioClientShareMode.Exclusive, false, 2);
                        waveProvider = new BufferedWaveProvider(WaveFormat.CreateCustomFormat(waveIn.WaveFormat.Encoding, waveIn.WaveFormat.SampleRate, waveIn.WaveFormat.Channels, waveIn.WaveFormat.AverageBytesPerSecond, waveIn.WaveFormat.BlockAlign, waveIn.WaveFormat.BitsPerSample));
                        waveProvider.DiscardOnBufferOverflow = true;
                        waveProvider.BufferDuration = TimeSpan.FromMilliseconds(80);
                        waveOut.Init(waveProvider);
                        waveOut.Play();
                        waveIn.DataAvailable += waveIn_DataAvailable;
                        waveIn.StartRecording();
                        ison = true;
                    }
                    catch
                    {
                        CloseWaves();
                        capturedevicefirst = true;
                        ison = false;
                        inc++;
                        if (inc > 1)
                        {
                            MessageBox.Show("Closing! You don't have a second audio card enable.");
                            closeonicon = true;
                            this.Close();
                        }
                    }
                    finally
                    {
                        Thread.Sleep(1000);
                    }
                }
                while (!ison & !closed);
            });
        }
        private void CloseWaves()
        {
            try
            {
                if (waveIn != null)
                {
                    waveIn.StopRecording();
                    waveIn.Dispose();
                }
                if (waveOut != null)
                {
                    waveOut.Stop();
                    waveOut.Dispose();
                }
                if (waveProvider != null)
                {
                    waveProvider = null;
                }
            }
            catch { }
        }
        private void waveIn_DataAvailable(object sender, WaveInEventArgs e)
        {
            waveProvider.AddSamples(e.Buffer, 0, e.BytesRecorded);
        }
        private void TrayMenuContext()
        {
            this.notifyIcon1.ContextMenuStrip = new System.Windows.Forms.ContextMenuStrip();
            this.notifyIcon1.ContextMenuStrip.Items.Add("Quit", null, this.MenuTest1_Click);
        }
        void MenuTest1_Click(object sender, EventArgs e)
        {
            closeonicon = true;
            this.Close();
        }
        private void MinimzedTray()
        {
            this.WindowState = FormWindowState.Minimized;
            this.ShowInTaskbar = false;
            this.Hide();
        }
        private void MaxmizedFromTray()
        {
            this.WindowState = FormWindowState.Normal;
            this.ShowInTaskbar = true;
            this.Show();
        }
        private void notifyIcon1_DoubleClick(object sender, EventArgs e)
        {
            MaxmizedFromTray();
        }
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!closeonicon)
            {
                e.Cancel = true;
                MinimzedTray();
                return;
            }
            closed = true;
            Thread.Sleep(100);
            CloseWaves();
        }
        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            closed = true;
            Thread.Sleep(100);
            CloseWaves();
        }
    }
}