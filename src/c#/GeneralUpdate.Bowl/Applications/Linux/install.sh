#!/bin/bash

# 获取脚本的实际名称
SCRIPT_NAME=$(basename "\$0")

# 检查是否提供了参数
if [ "$#" -ne 1 ]; then
    echo "Usage: $SCRIPT_NAME <package-file>"
    exit 1
fi

PACKAGE_FILE=\$1

# 检查文件类型并安装
if [[ "$PACKAGE_FILE" == *.rpm ]]; then
    if command -v rpm &> /dev/null; then
        sudo rpm -ivh "$PACKAGE_FILE" || { echo "Failed to install $PACKAGE_FILE using rpm"; exit 1; }
    elif command -v dnf &> /dev/null; then
        sudo dnf install -y "$PACKAGE_FILE" || { echo "Failed to install $PACKAGE_FILE using dnf"; exit 1; }
    elif command -v yum &> /dev/null; then
        sudo yum install -y "$PACKAGE_FILE" || { echo "Failed to install $PACKAGE_FILE using yum"; exit 1; }
    else
        echo "RPM package manager not found."
        exit 1
    fi
elif [[ "$PACKAGE_FILE" == *.deb ]]; then
    if command -v dpkg &> /dev/null; then
        sudo dpkg -i "$PACKAGE_FILE" || { echo "Failed to install $PACKAGE_FILE using dpkg"; exit 1; }
        sudo apt-get install -f -y || { echo "Failed to fix dependencies"; exit 1; }
    else
        echo "DEB package manager not found."
        exit 1
    fi
else
    echo "Unsupported package format."
    exit 1
fi

echo "Installation of $PACKAGE_FILE completed."