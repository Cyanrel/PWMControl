using System;
using System.Windows;
using System.Windows.Threading; // 引用定时器

namespace PwmControl
{
    public partial class ConfirmWindow : Window
    {
        private readonly DispatcherTimer _timer;
        private int _timeLeft = 11; // 倒计时秒数,记得去xaml同步修改

        public ConfirmWindow()
        {
            InitializeComponent();

            // 立即刷新一次 UI，防止界面刚出来显示默认文本
            UpdateUiText();

            // 初始化定时器
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            _timeLeft--;
            UpdateUiText();

            if (_timeLeft <= 0)
            {
                // 时间到，自动还原
                _timer.Stop();
                this.DialogResult = false;
                this.Close();
            }
        }
        private void UpdateUiText()
        {

            if (TxtMessage != null)
            {
                TxtMessage.Text = $"在 {_timeLeft} 秒内还原为以前的显示器设置。";
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
