# GeneralUpdate #
![](https://img.shields.io/github/license/JusterZhu/GeneralUpdate?color=blue)

![](imgs/GeneralUpdate_h.png)

## 1. Component Introduction ##

GeneralUpdate is an open-source automatic update component based on .NET Standard 2.0.

Documentation

- Official website: https://www.justerzhu.cn/
- Tutorial video: https://www.bilibili.com/video/BV1FT4y1Y7hV

Current Project GeneralUpdate

- https://github.com/JusterZhu/GeneralUpdate
- https://gitee.com/Juster-zhu/GeneralUpdate

MAUI GeneralUpdate.Maui

- https://github.com/GeneralLibrary/GeneralUpdate.Maui

Packaging Tool Project Address GeneralUpdate.Tools

- https://github.com/GeneralLibrary/GeneralUpdate.Tools
- https://gitee.com/GeneralTeam/GeneralUpdate.Tools

Sample Project Address GeneralUpdate-Samples

- https://github.com/GeneralLibrary/GeneralUpdate-Samples
- https://gitee.com/GeneralTeam/GeneralUpdate-Samples

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

| Operating System Name     | Supported      |
| ------------------------- | -------------- |
| Windows                   | Yes            |
| Linux                     | Yes            |
| Android                   | Yes            |
| Kylin V10 (Feiteng S2500) | Yes            |
| Kylin V10 (x64)           | Yes            |
| Ubuntu                    | Yes            |
| Loongson (Loongnix)       | To be verified |
