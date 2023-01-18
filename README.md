# GeneralUpdate #
![](https://img.shields.io/github/license/JusterZhu/GeneralUpdate?color=blue)


![](imgs/GeneralUpdate_h.png)

[English introduction](https://github.com/JusterZhu/GeneralUpdate/blob/master/README_en.md)

## 调查问卷

收集开源社区开发者的使用反馈以及新功能的建议。

https://wj.qq.com/s2/11454913/ddf6/

## 1.组件介绍 ##

GeneralUpdate寓意为通用更新力致于成为全平台更新组件。

| 功能                           | 是否支持 | 备注                                                         |
| ------------------------------ | -------- | ------------------------------------------------------------ |
| 断点续传                       | 支持     | 单次更新失败时，下次一次启动时继续上一次更新下载更新包内容。（引用组件默认生效） |
| 逐版本更新                     | 支持     | 客户端当前版本如果与服务器相差多个版本，则根据多个版本的发布日期逐个更新。（引用组件默认生效） |
| 二进制差分更新                 | 支持     | 对比新老版本通过差分算法生成补丁文件。（引用组件默认生效）   |
| 增量更新功能                   | 支持     | 相比上一个版本只更新当前修改过的文件。（引用组件默认生效）   |
| ~~配置文件保留更新~~           | ~~支持~~ | ~~暂时下线该功能~~                                           |
| 强制更新                       | 支持     | 打开客户端之后直接强制更新。                                 |
| 多分支更新                     | 支持     | 当一个产品有多个分支时，需要根据不同的分支更新对应的内容。   |
| 最新版本推送                   | 支持     | 基于SignalR实现，推送当前最新版本。                          |
| 客户端程序、服务端程序应用更新 | 支持     | C/S和B/S程序均可使用。                                       |
| 多平台、操作系统               | 部分支持 | Linux、Mac、Windows                                          |
| 多语言                         | 待验证   | 也可将本组件编写为控制台程序，作为更新“脚本”。更新其他语言的应用程序。 |
| 跳过更新                       | 支持     | 支持注入弹窗让用户决定是否更新本次发布，服务端决定强制时更新不生效。 |
| 相互升级                       | 支持     | 主程序可更新升级程序，升级程序可更新主程序。                 |



## 2.帮助文档 ##

- 讲解视频： https://www.bilibili.com/video/BV1aX4y137dd
- 官方网站： http://justerzhu.cn/
- 快速启动： https://mp.weixin.qq.com/s/pRKPFe3eC0NSqv9ixXEiTg
- 使用教程视频：https://www.bilibili.com/video/BV1FT4y1Y7hV
- 文档：https://gitee.com/GeneralLibrary/GeneralUpdate/tree/master/doc

## 3.开源地址 ##

### 3.1当前项目GeneralUpdate

- https://github.com/JusterZhu/GeneralUpdate
- https://gitee.com/Juster-zhu/GeneralUpdate

### 3.2打包工具项目地址GeneralUpdate.Tools

- https://github.com/GeneralLibrary/GeneralUpdate.Tools
- https://gitee.com/GeneralTeam/GeneralUpdate.Tools

### 3.3示例项目地址GeneralUpdate-Samples

- https://github.com/GeneralLibrary/GeneralUpdate-Samples

- https://gitee.com/GeneralTeam/GeneralUpdate-Samples



## 4.支持框架

| 框架名称                   | 是否支持 |
| -------------------------- | -------- |
| .NET Core 2.0              | 支持     |
| .NET 5 6 7 to last version | 支持     |
| .NET Framework 4.6.1       | 支持     |



| UI框架名称        | 是否支持                          |
| ----------------- | --------------------------------- |
| WPF               | 支持                              |
| UWP               | 商店模式下不可更新                |
| MAUI              | 支持Windows，Mac，（Linux未验证） |
| Avalonia          | 支持                              |
| WinUI             | 待验证，等待反馈                  |
| Console（控制台） | 支持                              |
| Winform           | 支持                              |



| 服务端框架 | 是否支持 |
| ---------- | -------- |
| ASP.NET    | 待验证   |



## 5.操作系统

| 操作系统名称 | 是否支持 |
| ------------ | -------- |
| Windows      | 支持     |
| Linux        | 支持     |
| Mac          | 支持     |
| iOS          | 暂不支持 |
| Android      | 暂不支持 |
| 树莓派(IoT)  | 暂不支持 |
| 麒麟V10(飞腾S2500)  | 支持   |
| 麒麟V10(x64)  | 支持   |
