using Accord.Video.FFMPEG;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using ValueStateChanged;

namespace RecordYoutube
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
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
        private void button1_Click(object sender, EventArgs e)
        {
            timer1 = new Timer();
            timer1.Interval = 20;
            timer1.Enabled = true;
            timer1.Tick += timer1_Tick;
            vf = new VideoFileWriter();
            vf.Open("output.avi", 800, 600, 25, VideoCodec.MPEG4, 1000000);
            timer1.Start();
        }
        private void button2_Click(object sender, EventArgs e)
        {
            timer1.Stop();
            vf.Close();
        }
        private void timer1_Tick(object sender, EventArgs e)
        {
            bp = new Bitmap(800, 600);
            gr = Graphics.FromImage(bp);
            gr.CopyFromScreen(0, 0, 0, 0, new Size(bp.Width, bp.Height));
            pictureBox1.Image = bp;
            pictureBox1.SizeMode = PictureBoxSizeMode.StretchImage;
            vf.WriteVideoFrame(bp);
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