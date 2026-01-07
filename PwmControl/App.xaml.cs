using PwmControl;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Windows;
using System.Threading;
using System.Diagnostics;
using System.IO;

namespace PwmControl
{
    public partial class App : Application
    {
        // 这里的路径不再是静态只读，因为我们需要在构造函数或启动时动态判断
        private string logPath = "";
        // // 日志路径：C:\Users\用户名\AppData\Local\PwmControl\PwmControl\boot_log.txt
        // private string logPath = Path.Combine(
        //     AppDomain.CurrentDomain.BaseDirectory,
        //     "boot_log.txt"
        //     );

        protected override void OnStartup(StartupEventArgs e)
        {
            // 【初始化日志路径】
            // 逻辑同 Config：如果 AppData 文件夹存在，就写在那；否则写在 EXE 旁边
            InitializeLogPath();

            base.OnStartup(e);

            // 检查是否是静默启动模式
            // e.Args 包含了命令行参数，比 Environment.GetCommandLineArgs 更方便
            bool isSilent = e.Args.Contains("-silent");

            if (isSilent)
            {
                // 解析当前的重试次数 (格式: -retry:1)
                int retryCount = 0;
                string? retryArg = e.Args.FirstOrDefault(a => a.StartsWith("-retry:"));
                if (retryArg != null)
                {
                    if (!int.TryParse(retryArg.Split(':')[1], out retryCount))
                    {
                        retryCount = 0; // 解析失败时的默认值
                    }
                }

                // 调用那个带参数的方法
                ExecuteSilentMode(retryCount);

                // 任务完成，退出
                Shutdown();
            }
            else
            {
                // 正常双击启动
                MainWindow mainWindow = new();
                mainWindow.Show();
            }
        }
        private void InitializeLogPath()
        {
            string appDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs", "PwmControl");
            string appDataLog = Path.Combine(appDataDir, "boot_log.txt");

            // 2. 本地目录
            string localDir = AppDomain.CurrentDomain.BaseDirectory;
            string localLog = Path.Combine(localDir, "boot_log.txt");

            // 判定逻辑：
            // 如果 AppData 的配置文件夹已经存在（说明已经安装或运行过），就写进那里
            // 避免在桌面生成 txt
            if (Directory.Exists(appDataDir))
            {
                logPath = appDataLog;
            }
            else
            {
                // B. 否则默认跟随 EXE
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop).TrimEnd('\\');
                string current = localDir.TrimEnd('\\');

                //if (current.Equals(desktop, StringComparison.OrdinalIgnoreCase))
                //{
                //    // 如果在桌面 -> 强制使用全局目录 (避免在桌面生成 txt)
                //    // 并确保目录被创建
                //    if (!Directory.Exists(globalDir)) Directory.CreateDirectory(globalDir);
                //    logPath = appDataLog;
                //}
                //else
                //{
                //    // C. 其他情况 (例如 D:\Tools\PwmControl) -> 跟随 EXE
                //    logPath = localLog;
                //}

                // 跟随 EXE
                logPath = localLog;

            }
        }
        private void ExecuteSilentMode(int currentRetry)
        {
            // 最大“转生”次数：5次
            // 每次间隔 5 秒，总共能覆盖开机后的 25-30 秒
            int maxRetries = 10;

            // 读取配置
            var config = ConfigManager.Load();
            int targetFreq = config.LastFrequency;
            if (targetFreq < 200) targetFreq = 1250;

            Log($"[启动 (第{currentRetry}次)] PID: {Environment.ProcessId}, 目标: {targetFreq}Hz");

            // 2. 动态缓冲策略 (越往后等越久)
            // 0次: 5秒 (冷启动缓冲)
            // 1次: 5秒 (刚失败，多歇会儿)
            // 2次: 8秒 (驱动可能真的慢)
            // 3次+: 10秒 (死磕)
            int sleepTime;
            if (currentRetry == 0) sleepTime = 1500;
            else if (currentRetry == 1) sleepTime = 1000;
            else sleepTime = 2000;

            //// 2. 缓冲等待
            //// 只有第0次（刚开机）需要长一点的缓冲，后续重试间隔短一点
            //int sleepTime = (currentRetry == 0) ? 5000 : 3000;
            Log($"[缓冲] 等待 {sleepTime / 1000} 秒...");
            Thread.Sleep(sleepTime);

            try
            {
                Log($"[初始化] 连接驱动...");
                // 每次进程启动，DLL 都是全新的，不会有缓存死锁问题
                var service = new PwmService();

                Log($"[探测] 读取频率...");
                var (currentFreq, _) = service.ReadFrequency();

                Log($"[探测] 成功, 当前: {currentFreq}Hz");

                Log($"[写入] 设置 {targetFreq}Hz...");
                service.SetFrequency(targetFreq);

                Log($"[成功] 成功运行");
            }
            catch (Exception ex)
            {
                Log($"[失败] 错误: {ex.Message}");

                // 失败处理：转生
                if (currentRetry < maxRetries)
                {
                    RestartSelf(currentRetry + 1);
                }
                else
                {
                    Log($"[放弃] 彻底超时");
                }
            }
        }
        // 启动一个新的自己，并传递 retry 参数
        private void RestartSelf(int nextRetryCount)
        {
            try
            {
                string currentExe = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                if (string.IsNullOrEmpty(currentExe)) return;

                ProcessStartInfo info = new ProcessStartInfo();
                info.FileName = currentExe;
                // 传递 -silent 和 新的 -retry:N 参数
                info.Arguments = $"-silent -retry:{nextRetryCount}";
                info.UseShellExecute = false; // 不使用 ShellExecute 避免弹窗
                info.CreateNoWindow = true;   // 彻底静默

                Process.Start(info);
            }
            catch (Exception ex)
            {
                Log($"[致命错误] 无法启动新进程: {ex.Message}");
            }
        }

        // 简单的日志辅助方法
        private void Log(string message)
        {
            try
            {
                // 追加写入日志
                File.AppendAllText(logPath, $"{DateTime.Now:HH:mm:ss.fff} - {message}\r\n");
            }
            catch
            { }
        }
    }
}

