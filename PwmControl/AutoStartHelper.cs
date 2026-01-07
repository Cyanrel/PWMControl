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
            string currentExe = Process.GetCurrentProcess().MainModule?.FileName ?? "";
            if (string.IsNullOrEmpty(currentExe)) return false;

            try
            {
                using var process = new Process();
                process.StartInfo.FileName = "powershell";
                // 核心指令：获取指定任务的“执行程序路径”
                // 如果任务不存在，Get-ScheduledTask 会报错（被 SilentlyContinue 吞掉），返回空
                // 如果存在，输出它的 Execute 属性（即 exe 路径）
                string cmd = $"$t = Get-ScheduledTask -TaskName '{TaskName}' -ErrorAction SilentlyContinue; if ($t) {{ $t.Actions.Execute }}";

                process.StartInfo.Arguments = $"-NoProfile -WindowStyle Hidden -Command \"{cmd}\"";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.CreateNoWindow = true;
                process.Start();

                // 读取输出并去除首尾空白（PowerShell 可能会带换行符）
                string output = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit();

                string currentFileName = Path.GetFileName(currentExe);

                // 判定条件改为：
                // 1. 任务路径里包含我当前的文件名 (例如 PwmControl-v0.2.exe)
                //    OR
                // 2. 任务路径里包含标准文件名 (PwmControl.exe)
                // 这样你改名测试时，勾选框依然会显示“已勾选”
                bool isSelf = output.IndexOf(currentFileName, StringComparison.OrdinalIgnoreCase) >= 0;
                bool isStandard = output.IndexOf("PwmControl.exe", StringComparison.OrdinalIgnoreCase) >= 0;

                return isSelf || isStandard;
            }
            catch
            {
                return false;
            }
        }

        public static void EnableAutoStart(string? customPath = null)
        {
            string exePath = customPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? "";
            if (string.IsNullOrEmpty(exePath)) return;

            // 创建基础任务
            string createCmd = $"/Create /TN \"{TaskName}\" /TR \"\\\"{exePath}\\\" -silent\" /SC ONLOGON /RL HIGHEST /F";
            RunSchTasks(createCmd);

            // PowerShell 修正电池策略
            string psCommand = $"$s = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit 00:00:00; Set-ScheduledTask -TaskName '{TaskName}' -Settings $s";

            RunPowerShell(psCommand);
        }

        public static void DisableAutoStart()
        {
            string cmd = $"/Delete /TN \"{TaskName}\" /F";
            RunSchTasks(cmd);
        }

        private static void RunSchTasks(string arguments)
        {
            using var process = new Process();
            process.StartInfo.FileName = "schtasks";
            process.StartInfo.Arguments = arguments;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            process.Start();
            process.WaitForExit();
        }

        private static void RunPowerShell(string command)
        {
            try
            {
                using var process = new Process();
                process.StartInfo.FileName = "powershell";
                process.StartInfo.Arguments = $"-NoProfile -WindowStyle Hidden -Command \"{command}\"";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                process.Start();
                process.WaitForExit();
            }
            catch { }
        }
    }
}