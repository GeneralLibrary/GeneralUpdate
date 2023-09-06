import subprocess

# 安装虚拟环境
subprocess.run(['python', '-m', 'venv', 'myenv'])

# 激活虚拟环境
subprocess.run(['source', 'myenv/bin/activate'])

# 使用pip安装软件包
subprocess.run(['pip', 'install', 'package-name'])

# 停用虚拟环境
subprocess.run(['deactivate'])
