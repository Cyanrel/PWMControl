using System;
using System.IO;
using System.Text.Json;

namespace PwmControl
{
    public class AppConfig
    {
        // 默认频率 1250
        public int LastFrequency { get; set; } = 1250;
    }

    public static class ConfigManager
    {

        // 缓存 JsonSerializerOptions 实例
        // 避免在每次 Save 时重复创建，提高性能并复用内部元数据缓存
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true
        };
        // 定义两个路径
        // 1. AppData 路径 (全局位置)
        private static readonly string AppDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs", "PwmControl", "config.json");

        // 2. EXE 旁边的路径 (便携/本地位置)
        private static readonly string LocalPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "config.json");

        /// <summary>
        /// 智能获取当前应该使用的配置文件路径
        /// </summary>
        private static string GetActiveConfigPath()
        {
            // 核心逻辑：
            // 如果 AppData 下的配置文件已经存在，就优先用它
            if (File.Exists(AppDataPath))
            {
                return AppDataPath;
            }

            // 如果 AppData 下没有，说明可能是第一次运行或者绿色版模式
            // 此时“跟随 EXE”，返回 EXE 所在的目录
            return LocalPath;
        }
        public static AppConfig Load()
        {
            try
            {
                string targetPath = GetActiveConfigPath();

                if (File.Exists(targetPath))
                {
                    string json = File.ReadAllText(targetPath);
                    var config = JsonSerializer.Deserialize<AppConfig>(json);
                    return config ?? new AppConfig();
                }
            }
            catch
            {
                // 读取失败返回默认
            }
            return new AppConfig();
        }

        public static void Save(int frequency)
        {
            try
            {
                string targetPath = GetActiveConfigPath();

                if (targetPath == LocalPath)
                {
                    string currentDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
                    string desktopDir = Environment.GetFolderPath(Environment.SpecialFolder.Desktop).TrimEnd('\\');

                    //// 比较路径：如果在桌面运行
                    //if (currentDir.Equals(desktopDir, StringComparison.OrdinalIgnoreCase))
                    //{
                    //    // 强制改写到 AppDataPath (Programs\PwmControl)
                    //    // 这样桌面就只会有一个 clean 的 EXE，不会生成 json
                    //    targetPath = AppDataPath;
                    //}
                }

                // 确保文件夹存在
                string? dir = Path.GetDirectoryName(targetPath);
                if (dir != null && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var config = new AppConfig { LastFrequency = frequency };
                File.WriteAllText(targetPath, JsonSerializer.Serialize(config, _jsonOptions));
            }
            catch
            {
                // 忽略保存错误
            }
        }
    }
}