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
        // 日志路径：C:\Users\用户名\AppData\Local\PwmControl\boot_log.txt
        private string logPath = Path.Combine(

            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PwmControl", "boot_log.txt");

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 1. 检查是否是静默启动模式
            // e.Args 包含了命令行参数，比 Environment.GetCommandLineArgs 更方便
            bool isSilent = e.Args.Contains("-silent");

            if (isSilent)
            {
                // 解析当前的重试次数 (格式: -retry:1)
                int retryCount = 0;
                string? retryArg = e.Args.FirstOrDefault(a => a.StartsWith("-retry:"));
                if (retryArg != null)
                {
                    int.TryParse(retryArg.Split(':')[1], out retryCount);
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
            if (currentRetry == 0) sleepTime = 5000;
            else if (currentRetry == 1) sleepTime = 5000;
            else if (currentRetry == 2) sleepTime = 8000;
            else sleepTime = 10000;

            //// 2. 缓冲等待
            //// 只有第0次（刚开机）需要长一点的缓冲，后续重试间隔短一点
            //int sleepTime = (currentRetry == 0) ? 5000 : 3000;
            Log($"[缓冲] 等待 {sleepTime / 1000} 秒...");
            Thread.Sleep(sleepTime);

            try
            {
                Log($"[初始化] 正在连接驱动...");
                // 每次进程启动，DLL 都是全新的，不会有缓存死锁问题
                var service = new PwmService();

                Log($"[探测] 正在探测当前频率...");
                var (currentFreq, _) = service.ReadFrequency();
                Log($"[探测] 驱动响应正常，当前频率: {currentFreq}Hz");

                Log($"[写入] 正在设置频率...");
                service.SetFrequency(targetFreq);

                Log($"[成功] 频率已设置为 {targetFreq} Hz！任务圆满结束。");


                //Log($"[写入] 正在设置频率...");
                //service.SetFrequency(targetFreq);

                //Log($"[成功] 频率已设置为 {targetFreq} Hz！任务圆满结束。");
                //// 成功了！直接结束，不需要做任何事
            }
            catch (Exception ex)
            {
                Log($"[失败] 错误: {ex.Message}");

                // 3. 失败处理：决定是否“转生”
                if (currentRetry < maxRetries)
                {
                    int nextRetry = currentRetry + 1;
                    Log($"[重试] 准备启动第 {nextRetry} 个新进程...");

                    RestartSelf(nextRetry);
                }
                else
                {
                    Log($"[放弃] 已达到最大重试次数 ({maxRetries})，驱动可能真的挂了。");
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
                Log($"[转生] 新进程已启动，当前进程即将自杀。");
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
                // 确保目录存在                           
                string? dir = Path.GetDirectoryName(logPath);
                if (dir != null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

                // 追加写入日志
                File.AppendAllText(logPath, $"{DateTime.Now:HH:mm:ss.fff} - {message}\r\n");
            }
            catch
            {
                // 写日志都失败了也就没办法了，忽略
            }
        }
    }
}

