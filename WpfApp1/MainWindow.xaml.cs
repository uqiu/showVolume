using NAudio.CoreAudioApi;
using System;
using System.Windows;
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
