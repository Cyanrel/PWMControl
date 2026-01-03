# PWMControl
一款现代化的PWM调节软件

## 初衷
想着利用起闲置的Surface Go 2
为了优化那坨💩山Windows11, 特地做了个优化软件\
唯独忘了还有这块社会你软哥, 一屏传四代的低频瞎眼PWM屏幕

市面上有一堆古董的win2000年代的PWM调节软件\
最高上限就是能用了😂

受不了花一晚上手搓了个

## 特点
- 好看, Fluent UI风格
- 无头静默自启, 开机占用小
- 针对`StartupDelay = 0`做了适配优化
  
## 限制
- Intel 四代 - 十代核显 (应该)
- Ultra200系列 不支持
- 最低 Windows 10
- 其他还在试

## 说明
- PWMControl-Lite.exe 需要依赖net8.0
- PWMControl.exe 可以独立运行
  
## 测试环境
- Surface Go 2 LCD
- Surface Laptop 5G LCD
  
目前还在测试阶段\
如果发现自启动不生效\
请发给我`C:\Users\你的用户名\AppData\Local\PwmControl\boot_log.txt`\
这个log文件

WIN UI3占用和体积刹不住\
还是用用WPF得了
