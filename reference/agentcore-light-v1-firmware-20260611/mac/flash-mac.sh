#!/usr/bin/env bash

set -euo pipefail

PORT="${1:-}"

echo "======================================"
echo "AgentCore-Light ESP32 固件升级工具"
echo "======================================"
echo

if [[ -z "$PORT" ]]; then
  echo "未传入串口设备。"
  echo "请先执行：ls /dev/tty.*"
  echo "然后再运行：./flash-mac.sh /dev/tty.usbmodemXXXX"
  exit 1
fi

if [[ ! -f firmware.bin ]]; then
  echo "当前目录未找到 firmware.bin"
  echo "请先把固件文件放到脚本同目录。"
  exit 1
fi

if [[ -f ./esptool.py ]]; then
  TOOL="python3 ./esptool.py"
elif command -v esptool.py >/dev/null 2>&1; then
  TOOL="esptool.py"
else
  echo "未找到 esptool.py"
  echo "请把 esptool.py 放到脚本同目录，或先执行：python3 -m pip install esptool"
  exit 1
fi

echo "正在向 $PORT 烧录 firmware.bin，请勿拔掉设备..."
echo

$TOOL --chip esp32c3 --port "$PORT" --baud 460800 write_flash 0x0 firmware.bin

echo
echo "固件升级完成。"
echo "请重新插拔设备后再测试。"
