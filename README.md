# GeneralUpdate #
![](https://img.shields.io/github/license/WELL-E/AutoUpdater?color=blue)


![](imgs/GeneralUpdate_h.png)

[English introduction](https://github.com/WELL-E/AutoUpdater/blob/master/README_en.md)

### 组件介绍 ###

GeneralUpdate寓意为通用更新力致于成为全平台更新组件。

| 功能                           | 是否支持 | 备注                                                         |
| ------------------------------ | -------- | ------------------------------------------------------------ |
| 断点续传                       | 支持     |                                                              |
| 逐版本更新                     | 支持     |                                                              |
| 二进制差分更新                 | 支持     |                                                              |
| 增量更新功能                   | 支持     |                                                              |
| 配置文件保留更新               | 支持     | 目前指支持深度为1的json配置文件                              |
| 强制更新                       | 支持     | 非强制更新可以弹出选择框供用户选择，强制更新则直接更新       |
| 多分支更新                     | 支持     | 当一个产品有多个分支时，需要根据不同的分支更新对应的内容     |
| 最新版本推送                   | 支持     | 基于SignalR实现                                              |
| 客户端程序、服务端程序应用更新 | 支持     | C/S和B/S程序均可使用                                         |
| 多平台、操作系统               | 部分支持 | Linux、Mac、Windows                                          |
| 多语言                         | 待验证   | 也可将本组件编写为控制台程序，作为更新“脚本”。更新其他语言的应用程序。 |



### 帮助文档 ###

- 讲解视频： https://www.bilibili.com/video/BV1aX4y137dd
- 官方网站： http://justerzhu.cn/
- 快速启动： https://mp.weixin.qq.com/s/pRKPFe3eC0NSqv9ixXEiTg
- 使用教程视频：https://www.bilibili.com/video/BV1FT4y1Y7hV

### 开源地址 ###

- https://github.com/JusterZhu/GeneralUpdate
- https://gitee.com/Juster-zhu/GeneralUpdate

### 支持框架

| 框架名称             | 是否支持 |
| -------------------- | -------- |
| .NET Core 2.0        | 支持     |
| .NET 5 6 7           | 支持     |
| .NET Framework 4.6.1 | 支持     |



| UI框架名称        | 是否支持            |
| ----------------- | ------------------- |
| WPF               | 支持                |
| UWP               | 商店模式下不可更新  |
| MAUI              | 正在兼容（windows） |
| Avalonia          | 支持                |
| WinUI             | 待验证，等待反馈    |
| Console（控制台） | 支持                |
| winform           | 支持                |



| 服务端框架 | 是否支持 |
| ---------- | -------- |
| ASP.NET    | 待验证   |



### 操作系统

| 操作系统名称 | 是否支持 |
| ------------ | -------- |
| Windows      | 支持     |
| Linux        | 支持     |
| Mac          | 支持     |
| iOS          | 暂不支持 |
| Android      | 暂不支持 |
| 树莓派(IoT)  | 待验证   |
