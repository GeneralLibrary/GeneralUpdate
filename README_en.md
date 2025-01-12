# GeneralUpdate #
![](https://img.shields.io/github/license/JusterZhu/GeneralUpdate?color=blue)

![](imgs/GeneralUpdate_h.png)

Unlimited Updates, Boundless Upgrades.

## 1. Component Introduction ##

**Updates Unlimited, Upgrades Unbounded.**

GeneralUpdate is an open-source cross-platform application auto-update component based on .NET Standard 2.0 and licensed under the MIT license.

| Repository            | Description                | URL                                                          |
| --------------------- | -------------------------- | ------------------------------------------------------------ |
| GeneralUpdate         | Auto Update                | https://github.com/GeneralLibrary/GeneralUpdate<br />https://gitee.com/GeneralLibrary/GeneralUpdate<br />https://gitcode.com/GeneralLibrary/GeneralUpdate |
| GeneralUpdate.Maui    | Maui Auto Update (Android) | https://github.com/GeneralLibrary/GeneralUpdate.Maui<br />https://gitcode.com/GeneralLibrary/GeneralUpdate-Maui |
| GeneralUpdate.Tools   | Update Patch Creation Tool | https://github.com/GeneralLibrary/GeneralUpdate.Tools<br />https://gitee.com/GeneralLibrary/GeneralUpdate.Tools<br />https://gitcode.com/GeneralLibrary/GeneralUpdate-Tools |
| GeneralUpdate-Samples | Usage Examples             | https://github.com/GeneralLibrary/GeneralUpdate-Samples<br />https://gitee.com/GeneralLibrary/GeneralUpdate-Samples<br />https://gitcode.com/GeneralLibrary/GeneralUpdate-Samples |

Help Documentation

- Official Website: https://www.justerzhu.cn/
- Tutorial Videos: https://www.bilibili.com/video/BV1c8iyYZE7P



Feature Introduction:

| Feature                    | Supported      | Remarks                                                      |
| -------------------------- | -------------- | ------------------------------------------------------------ |
| Resume Download            | Yes            | If an update fails, it continues from the last update on the next startup. (Enabled by default) |
| Version-by-Version Update  | Yes            | If the client is several versions behind the server, it updates one version at a time based on release dates. (Enabled by default) |
| Binary Differential Update | Yes            | Generates patch files using a differential algorithm between new and old versions. (Enabled by default) |
| Incremental Update         | Yes            | Only updates modified files compared to the previous version and deletes files not present in the current version. (Enabled by default) |
| Forced Update              | Yes            | Forces an update right after opening the client.             |
| Multi-Branch Update        | Yes            | Updates different content based on different branches of a product. |
| Latest Version Push        | Yes            | Implements Signal R to push the latest version.              |
| Multi-Language             | To be verified | Can also be written as a console application to update applications in other languages. |
| Skip Update                | Yes            | Allows injecting a prompt for users to decide whether to update; does not apply if forced by the server. |
| Mutual Upgrade             | Yes            | The main program can update the upgrade program, and vice versa. |
| Blacklist                  | Yes            | Skips files and file extensions listed in the blacklist during updates. |
| OSS                        | Yes            | Simplified update mechanism, requiring only a version.json file on the file server. Updates based on the version information in the configuration file. |
| Rollback and Backup        | Yes            | Backs up local client files before updating; rolls back if the client fails to start or crashes. |
| Driver Update              | To be verified | Backs up drivers locally before updating; rolls back if the client fails to start or crashes. |
| Custom Method List         | Yes            | Injects a custom method collection executed before the update starts. Notifies through exception subscription if any exception occurs. (Recommended to check the software environment before updating) |
| AOT                        | Yes            | Supports AOT compilation and release.                        |

## 2. Supported Frameworks

| .NET Framework Name        | Supported |
| -------------------------- | --------- |
| .NET Core 2.0              | Yes       |
| .NET 5 ... to last version | Yes       |
| .NET Framework 4.6.1       | Yes       |

| UI Framework Name | Supported                                |
| ----------------- | ---------------------------------------- |
| WPF               | Yes                                      |
| UWP               | Not updatable in store mode              |
| MAUI              | Currently supports Android platform only |
| Avalonia          | Yes                                      |
| WinUI             | Yes                                      |
| Console           | Yes                                      |
| WinForms          | Yes                                      |

## 3. Operating Systems

| Operating System Name       | Supported |
| --------------------------- | --------- |
| Windows                     | Yes       |
| Android (.NET MAUI)         | Yes       |
| Kylin V10 (Feiteng S2500)   | Yes       |
| Kylin V10 (Feiteng 2000)    | Yes       |
| Kylin V10 (x64)             | Yes       |
| Ubuntu 24.04.1 LTS          | Yes       |
| Loongnix (LoongArch 3A6000) | Yes       |
| HuaWeiEulerOS (Kunpeng)     | Yes       |
| Apple Mac (M1)              | Yes       |
| UOS (x64)                   | Yes       |
