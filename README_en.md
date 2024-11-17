# GeneralUpdate #
![](https://img.shields.io/github/license/JusterZhu/GeneralUpdate?color=blue)


![](imgs/GeneralUpdate_h.png)

### 1.Component introduction ###

- GeneralUpdate is an open source automatic upgrade component based on .NET Standard 2.0
- Operating environment：.NET7、.NET MAUI、Visual studio 2022(Preview)

| function                                          | Supported or not | note                                                         |
| ------------------------------------------------- | ---------------- | ------------------------------------------------------------ |
| Resume the upload at a breakpoint                 | yes              | If a single update fails, the contents of the update package will continue to download the previous update on the next startup. (Reference components take effect by default) |
| Update version-by-version                         | yes              | If the current version of the client differs from the server by more than one version, it is updated one by one according to the release date of the multiple versions. (Reference components take effect by default) |
| Binary differential update                        | yes              | Compare the old and new versions to generate patch files by differential algorithms. (Reference components take effect by default) |
| Incremental update functionality                  | yes              | Only the currently modified files are updated compared to the previous version，And delete files that do not exist in the current version. (Reference components take effect by default) |
| Force an update                                   | yes              | Force an update directly after opening the client.           |
| Multi-branch updates                              | yes              | When a product has multiple branches, you need to update the corresponding content according to different branches. |
| The latest version push                           | yes              | Based on the SignalR implementation, push the latest version. |
| Client programs and server programs apply updates | yes              | Both C/S and B/S programs are available.                     |
| Multi-platform, operating system                  | Partial support  | Windows、Android                                             |
| Multilingual                                      | To be verified   | You can also write this component as a console program as an update "script". Update applications in other languages. |
| Skip updates                                      | yes              | Support injection pop-up window allows users to decide whether to update this release, and the update will not take effect when the server decides to force it. |
| Upgrade each other                                | yes              | The main program updates the upgrade program, and the upgrade program updates the main program. |
| Black list                                        | yes              | Files and file extensions from the blacklist are skipped during the update process. |
| OSS                                               | yes              | Minimal updates require only the version configuration file of version.json to be placed on the file server. Components are updated and downloaded based on the version information in the configuration file.(Supported windows，MAUI Android) |
| Restore                                           | test             | Each version is backed up during a version-by-version update and rolled back version-by-version if the update fails. |
| Driver upgrade                                    | yes              | The driver file (.INF) of each version is backed up during the version-by-version update and is rolled back version-by-version if the update fails. |
| Will message                                      | test             | The upgrade is checked for success at boot and upgrade, and if it fails, the previous backup is restored according to the last word. The last word is that the will_message.json file in the C:\generalupdate_willmessages directory was automatically created before the update. will_message.json is about the file directory of the persistent rollback backup.(need to deploy GeneralUpdate. SystemService system service) |
| A list of custom methods                          | 支持             | Inject a custom collection of methods that are executed before the update starts. Execute a custom method list, and if there are any exceptions, you will be notified by exception subscription.(It is recommended to check the current software environment before updating) |



### 2.Help documentation ###

- explainer video： https://www.bilibili.com/video/BV1aX4y137dd
- Official website： http://justerzhu.cn/
- Quick Start： https://mp.weixin.qq.com/s/pRKPFe3eC0NSqv9ixXEiTg
- Use the tutorial video：https://www.bilibili.com/video/BV1FT4y1Y7hV
- Doc : https://gitee.com/GeneralLibrary/GeneralUpdate/tree/master/doc

### 3.Open source address ###

### 3.1 Current project GeneralUpdate

- https://github.com/JusterZhu/GeneralUpdate
- https://gitee.com/Juster-zhu/GeneralUpdate

### 3.2 Packaging tool project address GeneralUpdate.Tools

- https://github.com/GeneralLibrary/GeneralUpdate.Tools
- https://gitee.com/GeneralTeam/GeneralUpdate.Tools

### 3.3 Sample project address GeneralUpdate-Samples

- https://github.com/GeneralLibrary/GeneralUpdate-Samples

- https://gitee.com/GeneralTeam/GeneralUpdate-Samples

### 4.Support frame

| frame name                 | support |
| -------------------------- | ------- |
| .NET Core 2.0              | yes     |
| .NET 5 ... to last version | yes     |
| .NET Framework 4.6.1       | yes     |

| UI frame name | support                            |
| ------------- | ---------------------------------- |
| WPF           | yes                                |
| UWP           | Not updatable in store mode        |
| MAUI          | Compatible (Android)               |
| Avalonia      | yes                                |
| WinUI         | Not verified, waiting for feedback |
| Console       | yes                                |
| winform       | yes                                |

| server-side frame | support              |
| ----------------- | -------------------- |
| ASP.NET           | pending verification |

### Operating system

| operating system name | support                 |
| --------------------- | ----------------------- |
| Windows               | yes                     |
| Linux                 | pending verification    |
| Mac                   | pending verification    |
| iOS                   | Not currently supported |
| Android               | yes                     |
| raspberry pie         | Not currently supported |
| Kylin V10 (FT-S2500)  | yes                     |
| Kylin V10 (x64)       | yes                     |
