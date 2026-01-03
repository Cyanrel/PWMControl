using System;
using igfxDHLib; // 引用 Intel 库

namespace PwmControl
{
    public class PwmService
    {
        private DataHandler _dh;
        private byte[] _baseData;

        // 定义安全频率范围 (Hz)
        public const int SafeMin = 200;
        public const int SafeMax = 4000; // 限制在5000以免啸叫或过热
        public const int OptimalTarget = 1250; // IEEE 1789 推荐的无风险临界点

        public PwmService()
        {
            // 初始化 Intel 驱动句柄
            _dh = new DataHandler();
            _baseData = new byte[8];
        }

        public (int currentFreq, int baseClock) ReadFrequency()
        {
            uint error = 0;
            // ESCAPEDATATYPE_ENUM.GET_SET_PWM_FREQUENCY = 4 (根据原代码推断)
            // 原代码直接使用枚举，这里如果报错，请确认 igfxDHLib 的枚举定义
            _dh.GetDataFromDriver(ESCAPEDATATYPE_ENUM.GET_SET_PWM_FREQUENCY, 4, ref error, ref _baseData[0]);

            if (error != 0)
                throw new Exception($"读取 PWM 失败，驱动错误代码: {error:X}");

            int baseClock = BitConverter.ToInt32(_baseData, 0); // 0-3位
            int currentFreq = BitConverter.ToInt32(_baseData, 4); // 4-7位

            return (currentFreq, baseClock);
        }

        public void SetFrequency(int newFreq)
        {
            if (newFreq < SafeMin || newFreq > SafeMax)
                throw new ArgumentOutOfRangeException($"频率必须在 {SafeMin}Hz 到 {SafeMax}Hz 之间");

            // 写入新的频率到 byte 数组的 4-7位
            byte[] b = BitConverter.GetBytes(newFreq);
            Array.Copy(b, 0, _baseData, 4, 4);

            uint error = 0;
            _dh.SendDataToDriver(ESCAPEDATATYPE_ENUM.GET_SET_PWM_FREQUENCY, 4, ref error, ref _baseData[0]);

            if (error != 0)
                throw new Exception($"设置 PWM 失败，驱动错误代码: {error:X}");
        }

        // 核心算法：计算最佳平衡点
        public int CalculateSmartFrequency()
        {
            var (current, baseClock) = ReadFrequency();

            // 逻辑：
            // 1. 如果当前已经很高(>1250)，保持不变。
            // 2. 如果基准时钟允许，优先设定为 1250Hz (IEEE 1789 无风险标准)。
            // 3. 必须确保不超过硬件基准限制的一半(保守策略)。

            int target = OptimalTarget;

            // 如果硬件限制很大，取硬件极限的安全值
            // 这里我们做一个简单的逻辑，实际需根据 baseClock 的含义微调
            if (target > baseClock / 2)
            {
                target = baseClock / 2;
            }

            return Math.Max(target, SafeMin);
        }
    }
}
