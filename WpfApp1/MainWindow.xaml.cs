using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using System.Windows.Threading;
using System.Drawing;
// 确保使用正确的 FontStyle
using FontStyle = System.Drawing.FontStyle;
using System.Windows.Forms;
using System.Drawing.Text;
using System.Drawing.Drawing2D;
using System.Diagnostics; // 添加此行

namespace WpfApp1
{
    public partial class MainWindow : Window
    {
        private DispatcherTimer hideTimer;
        private MMDeviceEnumerator deviceEnumerator;
        private MMDevice device;
        private NotifyIcon notifyIcon;
        private Icon _currentIcon;

        public MainWindow()
        {
            try
            {
                InitializeComponent();
                
                // 初始化UI计时器
                hideTimer = new DispatcherTimer();
                hideTimer.Interval = TimeSpan.FromSeconds(2);
                hideTimer.Tick += HideTimer_Tick;

                // 初始化托盘图标
                InitializeNotifyIcon();

                // 设置窗口属性
                this.ShowInTaskbar = false;
                this.Topmost = true;
                
                // 注册加载事件
                Loaded += MainWindow_Loaded;
                
                // 初始化音频设备
                if (!InitializeAudioDevice())
                {
                    throw new Exception("音频设备初始化失败");
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"程序初始化失败:\n{GetDetailedErrorMessage(ex)}", 
                    "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                Environment.Exit(1); // 强制退出程序
            }
        }

        private string GetDetailedErrorMessage(Exception ex)
        {
            return $"错误类型: {ex.GetType().Name}\n" +
                   $"错误信息: {ex.Message}\n" +
                   $"堆栈跟踪: {ex.StackTrace}\n" +
                   (ex.InnerException != null ? $"内部错误: {ex.InnerException.Message}" : "");
        }

        private bool InitializeAudioDevice()
        {
            try
            {
                // 清理现有资源
                CleanupAudioDevice();

                // 创建设备枚举器
                try
                {
                    deviceEnumerator = new MMDeviceEnumerator();
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"创建设备枚举器失败:\n{GetDetailedErrorMessage(ex)}", 
                        "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    return false;
                }

                // 获取默认音频设备
                try
                {
                    device = deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                    if (device == null)
                    {
                        System.Windows.MessageBox.Show("未找到默认音频设备。", 
                            "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                        return false;
                    }

                    // 立即获取并设置当前音量
                    var volume = (int)(device.AudioEndpointVolume.MasterVolumeLevelScalar * 100);
                
                    Dispatcher.Invoke(() =>
                    {
                        UpdateVolume(volume);
                    });

                    // 注册音量变化事件
                    device.AudioEndpointVolume.OnVolumeNotification += AudioEndpointVolume_OnVolumeNotification;
                    
                    return true;
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"设备初始化失败: {ex.Message}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"初始化过程发生错误: {ex.Message}");
                return false;
            }
        }

        private void CleanupAudioDevice()
        {
            try
            {
                if (device != null)
                {
                    try
                    {
                        // 先取消事件订阅
                        if (device.AudioEndpointVolume != null)
                        {
                            device.AudioEndpointVolume.OnVolumeNotification -= AudioEndpointVolume_OnVolumeNotification;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"取消事件订阅失败: {ex.Message}");
                    }

                    try
                    {
                        // 分开处理设备释放
                        device.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"设备释放失败: {ex.Message}");
                    }
                    finally
                    {
                        device = null;
                    }
                }

                if (deviceEnumerator != null)
                {
                    try
                    {
                        // 使用 try-finally 确保引用被清空
                        Marshal.FinalReleaseComObject(deviceEnumerator);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"释放设备枚举器失败: {ex.Message}");
                    }
                    finally
                    {
                        deviceEnumerator = null;
                    }
                }
            }
            catch (Exception ex)
            {
                // 使用Debug.WriteLine代替MessageBox，避免UI阻塞
                Debug.WriteLine($"清理音频设备资源时发生错误: {ex.Message}");
            }
        }

        private void InitializeNotifyIcon()
        {
            notifyIcon = new NotifyIcon();
            notifyIcon.Icon = CreateIconWithText("0");
            notifyIcon.Visible = true;
            notifyIcon.Text = "Volume: 0%";
        }

        private Icon CreateIconWithText(string text)
        {
            if (_currentIcon != null)
            {
                _currentIcon.Dispose();
                _currentIcon = null;
            }

            Bitmap bitmap = null;
            IntPtr hIcon = IntPtr.Zero;

            try
            {
                bitmap = new Bitmap(32, 32);
                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    g.Clear(Color.Transparent);
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;

                    // 增大基础字体大小 (原有大小的 130%)
                    float fontSize = text.Length <= 2 ? 23.4f : 20.8f;  // 18 * 1.3 和 16 * 1.3
                    if (text.Length >= 3) fontSize = 18.2f;  // 14 * 1.3

                    using (var fontFamily = new FontFamily("Consolas"))  // 改为 Consolas 字体
                    using (var font = new Font(fontFamily, fontSize, System.Drawing.FontStyle.Bold, GraphicsUnit.Pixel))
                    {
                        SizeF textSize = g.MeasureString(text, font);
                
                        // 计算缩放比例，确保文字完全适应图标
                        float scale = Math.Min(28f / textSize.Width, 28f / textSize.Height) * 1.25f;
                        fontSize *= scale;

                        // 使用调整后的字体大小重新创建字体
                        using (var scaledFont = new Font(fontFamily, fontSize, System.Drawing.FontStyle.Bold, GraphicsUnit.Pixel))
                        {
                            textSize = g.MeasureString(text, scaledFont);
                            float x = (32f - textSize.Width) / 2;
                            float y = (32f - textSize.Height) / 2;

                            using (GraphicsPath path = new GraphicsPath())
                            {
                                path.AddString(
                                    text,
                                    fontFamily,
                                    (int)System.Drawing.FontStyle.Bold,
                                    fontSize,
                                    new PointF(x, y),
                                    StringFormat.GenericDefault
                                );

                                // 直接使用白色填充文字，不添加描边
                                using (Brush brush = new SolidBrush(Color.White))
                                {
                                    g.FillPath(brush, path);
                                }
                            }
                        }
                    }
                }

                hIcon = bitmap.GetHicon();
                _currentIcon = System.Drawing.Icon.FromHandle(hIcon);
                return _currentIcon;
            }
            finally
            {
                bitmap?.Dispose();
                if (hIcon != IntPtr.Zero && _currentIcon == null)
                {
                    NativeMethods.DestroyIcon(hIcon);
                }
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            MakeWindowOnAllDesktops(new WindowInteropHelper(this).Handle);
            deviceEnumerator.RegisterEndpointNotificationCallback(new AudioEndpointNotificationCallback(this));
        }

        private class NativeMethods
        {
            [DllImport("user32.dll", SetLastError = true)]
            public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

            [DllImport("user32.dll", SetLastError = true)]
            public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

            [DllImport("user32.dll")]
            public static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

            [DllImport("user32.dll")]
            public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

            [DllImport("user32.dll", SetLastError = true)]
            public static extern bool DestroyIcon(IntPtr hIcon);
        }

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;

        private void MakeWindowOnAllDesktops(IntPtr hWnd)
        {
            NativeMethods.SetWindowPos(hWnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
            IntPtr exStyle = NativeMethods.GetWindowLongPtr(hWnd, GWL_EXSTYLE);
            NativeMethods.SetWindowLongPtr(hWnd, GWL_EXSTYLE, new IntPtr(exStyle.ToInt64() | WS_EX_TOOLWINDOW));
        }

        private void AudioEndpointVolume_OnVolumeNotification(AudioVolumeNotificationData data)
        {
            OnVolumeChanged((int)(data.MasterVolume * 100));
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
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
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => UpdateVolume(volume));
                return;
            }

            try
            {
                VolumeBar.Value = volume;
                VolumeText.Text = volume.ToString();
                UpdateVolumeText();
                this.Show();
                hideTimer.Stop();
                hideTimer.Start();

                // 更新托盘图标，不显示百分号
                if (notifyIcon != null)
                {
                    notifyIcon.Icon = CreateIconWithText(volume.ToString());
                    notifyIcon.Text = $"Volume: {volume}%"; // 提示文本仍保留百分号
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"更新音量显示失败: {ex.Message}");
            }
        }

        public void OnVolumeChanged(int newVolume)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                UpdateVolume(newVolume);
            });
        }

        private void UpdateVolumeText()
        {
            VolumeText.Text = $"{VolumeBar.Value}%";
            
            // 等待布局更新完成
            VolumeText.UpdateLayout();
            
            // 计算文本应该的左边距，使其完全居中
            double windowWidth = this.ActualWidth;
            double textWidth = VolumeText.ActualWidth;
            double leftMargin = (windowWidth - textWidth) / 2;
            
            // 设置边距使文本完全居中
            VolumeText.Margin = new Thickness(leftMargin, VolumeText.Margin.Top, leftMargin, VolumeText.Margin.Bottom);
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            try
            {
                MakeWindowOnAllDesktops(new WindowInteropHelper(this).Handle);
                deviceEnumerator?.RegisterEndpointNotificationCallback(new AudioEndpointNotificationCallback(this));
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"窗口初始化失败: {ex.Message}", "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            try
            {
                if (_currentIcon != null)
                {
                    _currentIcon.Dispose();
                }
                
                if (notifyIcon != null)
                {
                    notifyIcon.Visible = false;
                    notifyIcon.Dispose();
                }

                if (device != null)
                {
                    device.AudioEndpointVolume.OnVolumeNotification -= AudioEndpointVolume_OnVolumeNotification;
                    device.Dispose();
                }

                if (deviceEnumerator != null)
                {
                    Marshal.ReleaseComObject(deviceEnumerator);
                }

                hideTimer?.Stop();
            }
            catch { }
            finally
            {
                base.OnClosed(e);
            }
        }

        private class AudioEndpointNotificationCallback : IMMNotificationClient
        {
            private readonly WeakReference<MainWindow> mainWindowRef;

            public AudioEndpointNotificationCallback(MainWindow mainWindow)
            {
                mainWindowRef = new WeakReference<MainWindow>(mainWindow);
            }

            public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
            {
                if (flow == DataFlow.Render && role == Role.Multimedia)
                {
                    if (mainWindowRef.TryGetTarget(out MainWindow mainWindow))
                    {
                        mainWindow.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            // 先清理旧设备
                            mainWindow.CleanupAudioDevice();
                            // 重新初始化并更新UI
                            if (mainWindow.InitializeAudioDevice())
                            {
                                // 获取并显示新设备的当前音量
                                try
                                {
                                    var currentVolume = (int)(mainWindow.device.AudioEndpointVolume.MasterVolumeLevelScalar * 100);
                                    mainWindow.UpdateVolume(currentVolume);

                                }
                                catch (Exception ex)
                                {
                                    System.Windows.MessageBox.Show($"更���音量失败: {ex.Message}");
                                }
                            }
                        }));
                    }
                }
            }

            public void OnDeviceAdded(string pwstrDeviceId) { }
            public void OnDeviceRemoved(string pwstrDeviceId) { }
            public void OnDeviceStateChanged(string pwstrDeviceId, DeviceState dwNewState) { }
            public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key) { }
        }
    }
}
