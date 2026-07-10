@echo off
setlocal

echo ======================================
echo AgentCore-Light ESP32 固件升级工具
echo ======================================
echo.
echo 使用前请确认：
echo 1. 已关闭控制台、浏览器串口、Arduino IDE 等占用串口的软件
echo 2. 已把 firmware.bin 和 esptool.exe 放在当前目录或固件包目录
echo.

set /p PORT=请输入设备串口号（例如 COM5）: 

if "%PORT%"=="" (
  echo 未输入串口号，已取消。
  pause
  exit /b 1
)

if not exist firmware.bin (
  echo 当前目录未找到 firmware.bin
  echo 请先把固件文件放到脚本同目录。
  pause
  exit /b 1
)

if not exist esptool.exe (
  echo 当前目录未找到 esptool.exe
  echo 请先把 esptool.exe 放到脚本同目录。
  pause
  exit /b 1
)

echo.
echo 正在向 %PORT% 烧录 firmware.bin，请勿拔掉设备...
echo.

esptool.exe --chip esp32c3 --port %PORT% --baud 460800 write_flash 0x0 firmware.bin

if errorlevel 1 (
  echo.
  echo 固件升级失败，请检查：
  echo - 串口号是否正确
  echo - 数据线是否支持传输
  echo - 是否有其他软件占用了串口
  pause
  exit /b 1
)

echo.
echo 固件升级完成。
echo 请重新插拔设备后再测试。
pause
