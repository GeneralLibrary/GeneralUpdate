# GeneralUpdate #
![](https://img.shields.io/github/license/WELL-E/AutoUpdater?color=blue)


![](imgs/GeneralUpdate_h.png)

### Component introduction ###

GeneralUpdate means that the general update is committed to becoming a full-platform update component, including features required for common personal and enterprise projects.

| Function                                          | support              | Remark                                                       |
| ------------------------------------------------- | -------------------- | ------------------------------------------------------------ |
| breakpoint resume                                 | yes                  |                                                              |
| Update by version                                 | yes                  |                                                              |
| binary difference update                          | yes                  |                                                              |
| Incremental update function                       | yes                  |                                                              |
| Configuration files keep updated                  | yes                  | Currently refers to the support for json configuration files with a depth of 1 |
| Force update                                      | yes                  | For non-forced updates, a selection box can pop up for users to choose from, while for forced updates, they are updated directly. |
| Multi-branch update                               | yes                  | When a product has multiple branches, the corresponding content needs to be updated according to different branches |
| Push the latest version                           | yes                  | Implementation based on SignalR                              |
| Client program, server program application update | yes                  | Both C/S and B/S programs can be used                        |
| Multi-platform, operating system                  | yes                  | Linux、Mac、Windows                                          |
| multi-language                                    | pending verification | This component can also be written as a console program as an update "script". Update apps in other languages. |



### Help documentation ###

- explainer video： https://www.bilibili.com/video/BV1aX4y137dd
- Official website： http://justerzhu.cn/
- Quick Start： https://mp.weixin.qq.com/s/pRKPFe3eC0NSqv9ixXEiTg
- Use the tutorial video：https://www.bilibili.com/video/BV1FT4y1Y7hV

### Open source address ###

- https://github.com/JusterZhu/GeneralUpdate
- https://gitee.com/Juster-zhu/GeneralUpdate

### Support frame

| frame name           | support |
| -------------------- | ------- |
| .NET Core 2.0        | yes     |
| .NET 5 6 7           | yes     |
| .NET Framework 4.6.1 | yes     |

| UI frame name | support                            |
| ------------- | ---------------------------------- |
| WPF           | yes                                |
| UWP           | Not updatable in store mode        |
| MAUI          | Compatible (windows)               |
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
| Linux                 | yes                     |
| Mac                   | yes                     |
| iOS                   | Not currently supported |
| Android               | Not currently supported |
| raspberry pie         | pending verification    |
