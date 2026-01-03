using System;
using System.Windows;
using System.Windows.Threading; // 引用定时器

namespace PwmControl
{
    public partial class ConfirmWindow : Window
    {
        private DispatcherTimer _timer;
        private int _timeLeft = 15; // 倒计时秒数

        public ConfirmWindow()
        {
            InitializeComponent();

            // 初始化定时器
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            _timeLeft--;
            TxtTimer.Text = $"{_timeLeft} 秒后自动还原";
            TxtMessage.Text = $"如果屏幕正常显示，请在 {_timeLeft} 秒内点击确认";

            if (_timeLeft <= 0)
            {
                // 时间到！视为测试失败，返回 false
                _timer.Stop();
                this.DialogResult = false;
                this.Close();
            }
        }

        private void BtnKeep_Click(object sender, RoutedEventArgs e)
        {
            // 用户点击确认，测试通过
            _timer.Stop();
            this.DialogResult = true;
            this.Close();
        }

        private void BtnRevert_Click(object sender, RoutedEventArgs e)
        {
            // 用户点击还原
            _timer.Stop();
            this.DialogResult = false;
            this.Close();
        }
    }
}
