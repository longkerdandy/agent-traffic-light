#!/usr/bin/env python3

import runpy
import subprocess
import sys


def ensure_esptool():
    try:
        __import__("esptool")
    except ImportError:
        print("未检测到 esptool，正在尝试自动安装...")
        subprocess.check_call([sys.executable, "-m", "pip", "install", "esptool"])


def main():
    ensure_esptool()
    runpy.run_module("esptool", run_name="__main__")


if __name__ == "__main__":
    main()
