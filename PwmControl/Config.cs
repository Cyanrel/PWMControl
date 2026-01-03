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
        // 配置文件固定存放位置：C:\Users\用户名\AppData\Local\PwmControl\config.json
        // 这样无论 exe 在哪里，读取的都是同一个配置
        private static readonly string ConfigFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PwmControl");

        private static readonly string ConfigPath = Path.Combine(ConfigFolder, "config.json");

        public static AppConfig Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath);
                    var config = JsonSerializer.Deserialize<AppConfig>(json);
                    return config ?? new AppConfig();
                }
            }
            catch
            {
                // 如果读取失败（文件损坏等），返回默认值
            }
            return new AppConfig();
        }

        public static void Save(int frequency)
        {
            try
            {
                if (!Directory.Exists(ConfigFolder))
                {
                    Directory.CreateDirectory(ConfigFolder);
                }

                var config = new AppConfig { LastFrequency = frequency };
                string json = JsonSerializer.Serialize(config);
                File.WriteAllText(ConfigPath, json);
            }
            catch
            {
                // 保存失败忽略，或者弹窗提示
            }
        }
    }
}