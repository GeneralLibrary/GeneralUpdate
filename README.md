# GeneralUpdate #
![](https://img.shields.io/github/license/JusterZhu/GeneralUpdate?color=blue)


![](imgs/GeneralUpdate_h.png)

[English introduction](https://github.com/JusterZhu/GeneralUpdate/blob/master/README_en.md)

## 1.组件介绍 ##

- GeneralUpdate是一款基于.NET Standard2.0开源自动升级组件。
- 运行环境：.NET7、.NET MAUI、Visual studio 2022(Preview)

| 功能                           | 是否支持 | 备注                                                         |
| ------------------------------ | -------- | ------------------------------------------------------------ |
| 断点续传                       | 支持     | 单次更新失败时，下次一次启动时继续上一次更新下载更新包内容。（引用组件默认生效） |
| 逐版本更新                     | 支持     | 客户端当前版本如果与服务器相差多个版本，则根据多个版本的发布日期逐个更新。（引用组件默认生效） |
| 二进制差分更新                 | 支持     | 对比新老版本通过差分算法生成补丁文件。（引用组件默认生效）   |
| 增量更新功能                   | 支持     | 相比上一个版本只更新当前修改过的文件，并且删除当前版本不存在的文件。（引用组件默认生效） |
| 强制更新                       | 支持     | 打开客户端之后直接强制更新。                                 |
| 多分支更新                     | 支持     | 当一个产品有多个分支时，需要根据不同的分支更新对应的内容。   |
| 最新版本推送                   | 支持     | 基于Signal R实现，推送当前最新版本。                         |
| 客户端程序、服务端程序应用更新 | 支持     | C/S和B/S程序均可使用。                                       |
| 多平台、操作系统               | 部分支持 | Windows、MAUI Android平台                                    |
| 多语言                         | 待验证   | 也可将本组件编写为控制台程序，作为更新“脚本”。更新其他语言的应用程序。 |
| 跳过更新                       | 支持     | 支持注入弹窗让用户决定是否更新本次发布，服务端决定强制时更新不生效。 |
| 相互升级                       | 支持     | 主程序可更新升级程序，升级程序可更新主程序。                 |
| 黑名单                         | 支持     | 在更新过程中会跳过黑名单中的文件列表和文件扩展名列表。       |
| OSS                            | 支持     | 极简化更新，是一套独立的更新机制。只需要在文件服务器中放置version.json的版本配置文件。组件会根据配置文件中的版本信息进行更新下载。(支持Windows，MAUI Android) |
| 回滚                           | 待测试   | 逐版本更新时会备份每个版本，如果更新失败则逐版本回滚。       |
| 驱动更新                       | 支持     | 逐版本更新时会备份每个版本的驱动文件（.inf），如果更新失败则逐版本回滚。 |
| 遗言                           | 待测试   | 开机时和升级时会检查升级是否成功，如果失败则根据遗言还原之前的备份。遗言是更新之前就已经自动创建在C:\generalupdate_willmessages目录下的will_message.json文件。will_message.json的内容是持久化回滚备份的文件目录相关信息。（需要部署GeneralUpdate.SystemService系统服务） |
| 自定义方法列表                 | 支持     | 注入一个自定义方法集合，该集合会在更新启动前执行。执行自定义方法列表如果出现任何异常，将通过异常订阅通知。（推荐在更新之前检查当前软件环境） |



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
| .NET 5 ... to last version | 支持     |
| .NET Framework 4.6.1       | 支持     |

| UI框架名称        | 是否支持              |
| ----------------- | --------------------- |
| WPF               | 支持                  |
| UWP               | 商店模式下不可更新    |
| MAUI              | 目前仅支持Android平台 |
| Avalonia          | 支持                  |
| WinUI             | 待验证，等待反馈      |
| Console（控制台） | 支持                  |
| Winform           | 支持                  |

| 服务端框架 | 是否支持 |
| ---------- | -------- |
| ASP.NET    | 待验证   |



## 5.操作系统

| 操作系统名称 | 是否支持 |
| ------------ | -------- |
| Windows      | 支持     |
| Linux        | 待验证 |
| Mac          | 待验证 |
| Android      | 支持 |
| 树莓派(IoT)  | 待验证 |
| 麒麟V10(飞腾S2500)  | 支持   |
| 麒麟V10(x64)  | 支持   |



## 6.GeneralUpdate.SystemService发布/部署

GeneralUpdate.SystemService是一个windows系统服务，并不是部署在服务端的web api。它的主要作用是监听更新过程，以及更新崩溃之后还原。

**发布：**

推荐发布Single file，如果想发布AOT版本需要移除源码中映射代码。

```shell
dotnet publish -r win-x64 -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true --self-contained true
```

**创建/部署windows服务：**

```shell
sc create MyWorkerService binPath="C:\your_path\GeneralUpdate.SystemService.exe"
```

**启动已部署的windows服务：**

```shell
sc start GeneralUpdate.SystemService
```

**删除已部署的windows服务：**

```shell
sc delete GeneralUpdate.SystemService
```

