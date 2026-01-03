using System;
using System.Diagnostics;
using System.Security.Principal;
using System.Reflection;
using System.IO;

namespace PwmControl
{
    public static class AutoStartHelper
    {
        private const string TaskName = "PwmControlAutoStart";

        // 检查当前是否已经开启了自启动
        public static bool IsAutoStartEnabled()
        {
            using (var process = new Process())
            {
                process.StartInfo.FileName = "schtasks";
                process.StartInfo.Arguments = $"/Query /TN \"{TaskName}\"";
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.Start();
                process.WaitForExit();

                // 如果退出代码是0，说明任务存在
                return process.ExitCode == 0;
            }
        }

        // 开启自启动 (创建最高权限任务)
        // 如果不传参数，就默认用当前程序的路径
        public static void EnableAutoStart(string? customPath = null)
        {
            string exePath = customPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? "";

            if (string.IsNullOrEmpty(exePath)) return;

            // 依然加上 -silent 参数
            string cmd = $"/Create /TN \"{TaskName}\" /TR \"\\\"{exePath}\\\" -silent\" /SC ONLOGON /RL HIGHEST /F";

            RunSchTasks(cmd);
        }

        // 关闭自启动
        public static void DisableAutoStart()
        {
            string cmd = $"/Delete /TN \"{TaskName}\" /F";
            RunSchTasks(cmd);
        }

        private static void RunSchTasks(string arguments)
        {
            using (var process = new Process())
            {
                process.StartInfo.FileName = "schtasks";
                process.StartInfo.Arguments = arguments;
                // 关闭 ShellExecute，直接启动进程
                process.StartInfo.UseShellExecute = false;

                // 彻底禁止创建窗口
                process.StartInfo.CreateNoWindow = true;
               
                process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                try
                {
                    process.Start();
                    process.WaitForExit();
                }
                catch (Exception ex)
                {
                    throw new Exception("无法设置自启动，请确保主程序已获管理员权限。\n" + ex.Message);
                }
            }
        }
    }
}