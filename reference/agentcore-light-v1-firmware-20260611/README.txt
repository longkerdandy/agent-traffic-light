AgentCore-Light v1 固件包

文件说明：

1. windows/flash-windows.bat
   Windows 一键烧录脚本

2. windows/firmware.bin
   Windows 使用的 ESP32-C3 固件文件

3. windows/esptool.exe
   Windows 烧录工具

4. mac/flash-mac.sh
   Mac 一键烧录脚本

5. mac/firmware.bin
   Mac 使用的 ESP32-C3 固件文件

6. mac/esptool.py
   Mac 烧录工具启动脚本

使用建议：

- Windows 用户先看说明网站中的“ESP32 固件升级教程”
- Mac 用户先执行 ls /dev/tty.* 查看设备端口
- 烧录前关闭浏览器串口、控制台、Arduino IDE、VSCode 串口监视器
- 烧录过程中不要拔掉设备
