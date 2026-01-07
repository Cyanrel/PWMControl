using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;

namespace PwmControl
{
    public partial class MainWindow : Window
    {
        private readonly PwmService? _pwmService;

        public MainWindow()
        {
            InitializeComponent();
            try
            {
                _pwmService = new PwmService();
                RefreshData();
            }
            catch (Exception ex)
            {
                MessageBox.Show("初始化驱动失败，请确认这是 Intel 显卡设备。\n" + ex.Message);
                TxtStatus.Text = "驱动连接失败";
                TxtStatus.Foreground = Brushes.Red;
                BtnApply.IsEnabled = false;
            }

            // 启动时读取全局配置
            var config = ConfigManager.Load();
            InputFreq.Text = config.LastFrequency.ToString();

            // 检查自启动状态并更新UI
            UpdateAutoStartStatus();


        }

        private void RefreshData()
        {
            if (_pwmService == null) return;
            try
            {
                var (current, baseClock) = _pwmService.ReadFrequency();
                TxtCurrentFreq.Text = current.ToString();

                // 简单的状态判断
                if (current > 0 && current < 1249)
                {
                    TxtStatus.Text = "舒服? 低频PWM你舒服?! ";
                    TxtStatus.Foreground = Brushes.OrangeRed;
                }
                else if (current >= 1249 && current < 2999)
                {
                    TxtStatus.Text = "刚刚起步, 没劲儿啊";
                    TxtStatus.Foreground = Brushes.Green;
                }
                else if (current == 0)
                {
                    TxtStatus.Text = "恭喜你这是一块无频闪的绝世好屏";
                    TxtStatus.Foreground = Brushes.Green;
                }
                else 
                { 
                    TxtStatus.Text = "这是我最后的高频PWM啦";
                    TxtStatus.Foreground = Brushes.Green;
                }
            }
            catch (Exception ex)
            {
                TxtStatus.Text = "读取失败: " + ex.Message;
            }
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            RefreshData();
        }

        private void BtnSmart_Click(object sender, RoutedEventArgs e)
        {
            if (_pwmService == null) return;

            try
            {
                int smartFreq = _pwmService.CalculateSmartFrequency();
                InputFreq.Text = smartFreq.ToString();
                TxtStatus.Text = $"已计算最佳平衡点: {smartFreq}Hz";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            // 如果服务没初始化，直接返回
            if (_pwmService == null)
            {
                MessageBox.Show("驱动未初始化，无法应用设置。");
                return;
            }

            if (int.TryParse(InputFreq.Text, out int newFreq))
            {
                int oldFreq = 0;
                try
                {
                    // 先读取并保存旧频率
                    var (current, _) = _pwmService.ReadFrequency();
                    oldFreq = current;

                    // 如果新旧频率一样，就变
                    if (oldFreq == newFreq)
                    {
                        MessageBox.Show("当前已经是这个频率了。");
                        return;
                    }

                    // 3. 应用新频率！(此时可能会黑屏)
                    _pwmService.SetFrequency(newFreq);

                    // 4. 立即弹出倒计时窗口（模态窗口，会阻塞代码执行，直到窗口关闭）
                    // 只要屏幕没黑，用户就能看到这个窗口
                    ConfirmWindow confirmDlg = new();
                    bool? result = confirmDlg.ShowDialog();

                    // 5. 判断结果
                    if (result == true)
                    {
                        // 用户点击了“确认保留”
                        RefreshData(); // 刷新界面显示

                        // 用 ConfigManager 保存到全局位置
                        ConfigManager.Save(newFreq);

                        MessageBox.Show("设置成功并已保存！下次开机将自动应用此频率。", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        // 结果是 false (时间到了 或 用户点了还原)
                        // 救命！还原旧频率！
                        _pwmService.SetFrequency(oldFreq);
                        RefreshData();
                        MessageBox.Show("已自动还原到之前的频率。", "安全回滚", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                catch (Exception ex)
                {
                    // 发生异常时，尝试尽力还原
                    try { if (oldFreq > 0) _pwmService.SetFrequency(oldFreq); } catch { }

                    MessageBox.Show("设置失败: " + ex.Message);
                    RefreshData();
                }
            }
            else
            {
                MessageBox.Show("请输入有效的数字");
            }
        }
        private void UpdateAutoStartStatus()
        {
            try
            {
                // 获取状态
                bool isEnabled = AutoStartHelper.IsAutoStartEnabled();

                // 【自愈逻辑】
                // 如果系统认为开启了自启，为了防止“僵尸任务”（任务指向了不存在的旧路径），
                // 我们在这里“静默”地重新注册一次当前路径。
                // 这样可以保证：只要勾是打上的，路径就一定是活的。
                if (isEnabled)
                {
                    string? currentPath = Process.GetCurrentProcess().MainModule?.FileName;
                    if (!string.IsNullOrEmpty(currentPath))
                    {
                        string currentName = Path.GetFileName(currentPath);

                        // 只有当当前运行的文件名就是 "PwmControl.exe" 时，才去执行路径修正。
                        // 如果你是 "PWMControl-v0.2.exe"，说明你在测试，
                        // 此时虽然 UI 显示已勾选（因为 AutoStartHelper 兼容了），但不要去乱改后台的任务路径。
                        if (currentName.Equals("PwmControl.exe", StringComparison.OrdinalIgnoreCase))
                        {
                            // 再次确认：只有路径真的变了才去覆盖，减少磁盘写入
                            AutoStartHelper.EnableAutoStart(currentPath);
                        }
                    }
                }

                // 2. 更新 UI (防止触发事件)
                ChkAutoStart.Checked -= ChkAutoStart_Checked;
                ChkAutoStart.Unchecked -= ChkAutoStart_Unchecked;

                ChkAutoStart.IsChecked = isEnabled;

                ChkAutoStart.Checked += ChkAutoStart_Checked;
                ChkAutoStart.Unchecked += ChkAutoStart_Unchecked;
            }
            catch { }
        }

        // 用户勾选时
        // 用户勾选时触发
        private void ChkAutoStart_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                // 1. 获取当前路径和理想路径
                string currentExePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";

                if (string.IsNullOrEmpty(currentExePath))
                {
                    MessageBox.Show("无法获取当前程序路径，无法设置自启动。");
                    ChkAutoStart.IsChecked = false;
                    return;
                }
                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string targetDir = Path.Combine(appDataPath, "Programs", "PwmControl"); // 目标文件夹
                string targetExePath = Path.Combine(targetDir, "PwmControl.exe");       // 目标文件

                // 2. 判断：如果当前不在理想路径，且理想路径里没有这个文件
                // (StringComparison.OrdinalIgnoreCase 用于忽略大小写比较)
                bool isAlreadyInPlace = currentExePath.Equals(targetExePath, StringComparison.OrdinalIgnoreCase);

                if (!isAlreadyInPlace)
                {
                    // 3. 弹窗询问
                    var result = MessageBox.Show(
                        "检测到程序当前未运行在标准目录。\n\n" +
                        "为了确保开机自启动稳定运行，\n" +
                        "建议将程序移动到系统的 %LocalAppData% 目录。\n\n" +
                        "是否立即移动并设置自启动？",
                        "优化运行位置",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        // === 用户同意搬家 ===
                        PerformMoveAndRestart(targetDir, targetExePath, currentExePath);
                        return; // 移动后旧程序会退出，所以这里直接返回
                    }
                }

                // === 情况 A：用户已经在正确位置 ===
                // === 情况 B：用户拒绝搬家，硬要在当前位置自启 ===
                AutoStartHelper.EnableAutoStart(currentExePath);
                MessageBox.Show("已开启开机自启！\n下次开机将以静默模式运行。", "设置成功");
            }
            catch (Exception ex)
            {
                MessageBox.Show("设置失败: " + ex.Message);
                // 如果失败，把勾去掉，避免误导用户
                ChkAutoStart.IsChecked = false;
            }
        }

        // 独立的搬家逻辑，保持代码整洁
        private void PerformMoveAndRestart(string targetDir, string targetExePath, string currentExePath)
        {
            try
            {
                // 创建目标目录
                if (!Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }

                // 复制 exe 文件
                File.Copy(currentExePath, targetExePath, true);


                // 直接给新位置的文件设置自启动
                AutoStartHelper.EnableAutoStart(targetExePath);

                // 启动新位置的程序
                Process.Start(new ProcessStartInfo
                {
                    FileName = targetExePath,
                    UseShellExecute = true
                });

                // 关闭旧程序
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                MessageBox.Show("移动失败，将保持在当前位置运行。\n错误信息: " + ex.Message);
                // 移动失败，回退到给当前路径设置自启
                AutoStartHelper.EnableAutoStart(currentExePath);
            }
        }

        // 用户取消勾选时
        private void ChkAutoStart_Unchecked(object sender, RoutedEventArgs e)
        {
            try
            {
                AutoStartHelper.DisableAutoStart();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
    }
}