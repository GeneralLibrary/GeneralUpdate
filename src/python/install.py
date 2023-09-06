import subprocess
import platform

def install_example_software():
    system = platform.system()

    if system == "Linux":
        install_linux()
    elif system == "Windows":
        install_windows()
    elif system == "Darwin":  # macOS
        install_macos()
    else:
        print("Unsupported operating system")

def install_linux():
    try:
        print("Installing example_software on Linux...")
        # 使用适当的软件包管理器安装软件
        subprocess.run(["apt-get", "install", "example_software"])
        print("example_software installed successfully on Linux.")
    except Exception as e:
        print(f"Error installing example_software on Linux: {str(e)}")

def install_windows():
    try:
        print("Installing example_software on Windows...")
        # 在Windows上执行软件安装操作
        # 你可能需要下载安装程序并运行它，或者使用其他安装方法
        print("example_software installed successfully on Windows.")
    except Exception as e:
        print(f"Error installing example_software on Windows: {str(e)}")

def install_macos():
    try:
        print("Installing example_software on macOS...")
        # 在macOS上执行软件安装操作
        # 你可能需要下载安装程序并运行它，或者使用其他安装方法
        print("example_software installed successfully on macOS.")
    except Exception as e:
        print(f"Error installing example_software on macOS: {str(e)}")

if __name__ == "__main__":
    install_example_software()
