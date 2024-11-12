using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using NAudio.CoreAudioApi;
using System.Windows.Threading;

namespace WpfApp1
{
    public partial class MainWindow : Window
    {
        private DispatcherTimer hideTimer;
        private MMDeviceEnumerator deviceEnumerator;
        private MMDevice device;

        public MainWindow()
        {
            InitializeComponent();
            hideTimer = new DispatcherTimer();
            hideTimer.Interval = TimeSpan.FromSeconds(2);
            hideTimer.Tick += HideTimer_Tick;

            deviceEnumerator = new MMDeviceEnumerator();
            device = deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            device.AudioEndpointVolume.OnVolumeNotification += AudioEndpointVolume_OnVolumeNotification;

            this.ShowInTaskbar = false; // 隐藏任务栏图标
            this.Topmost = true; // 窗口始终在最前面
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            MakeWindowOnAllDesktops(new WindowInteropHelper(this).Handle);
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;

        private void MakeWindowOnAllDesktops(IntPtr hWnd)
        {
            SetWindowPos(hWnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
            IntPtr exStyle = GetWindowLongPtr(hWnd, GWL_EXSTYLE);
            SetWindowLongPtr(hWnd, GWL_EXSTYLE, new IntPtr(exStyle.ToInt64() | WS_EX_TOOLWINDOW));
        }

        private void AudioEndpointVolume_OnVolumeNotification(AudioVolumeNotificationData data)
        {
            OnVolumeChanged((int)(data.MasterVolume * 100));
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 设置窗口位置
            var desktopWorkingArea = SystemParameters.WorkArea;
            this.Left = (desktopWorkingArea.Width - this.Width) / 2;
            this.Top = desktopWorkingArea.Bottom - this.Height - 10;
            this.Hide();

            UpdateVolumeText();
        }

        private void HideTimer_Tick(object? sender, EventArgs e)
        {
            this.Hide();
            hideTimer.Stop();
        }

        public void UpdateVolume(int volume)
        {
            VolumeBar.Value = volume;
            VolumeText.Text = volume.ToString();
            this.Show();
            hideTimer.Stop();
            hideTimer.Start();
        }

        // 模拟音量变化事件处理程序
        public void OnVolumeChanged(int newVolume)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                UpdateVolume(newVolume);
            });
        }

        private void UpdateVolumeText()
        {
            VolumeText.Text = $"{VolumeBar.Value}%";
            // 根据进度条的值更新 TextBlock 的位置
            VolumeText.Margin = new Thickness(VolumeBar.Value * (VolumeBar.Width / VolumeBar.Maximum) - VolumeText.ActualWidth / 2, 0, 0, 0);
        }
    }
}
