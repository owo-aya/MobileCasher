# 手机收款 / MobileCasher

《Probably Stolen》的局域网手机收款播报模组。游戏里成交，手机显示顾客头像、名字和实际到账金额，并播放支付宝或微信的到账语音。

## 创意工坊

[Steam 创意工坊页面（仅好友可见）](https://steamcommunity.com/sharedfiles/filedetails/?id=3835402643)

需要前置「到账语音播报 / Announce the Transaction」，当前适配版本为 **1.1.1**。[前置创意工坊页面](https://steamcommunity.com/sharedfiles/filedetails/?id=3831162516)

## 功能

- 手机显示游戏顾客信息、实际到账金额和对应平台的模拟收款界面。
- 到账语音由手机播放，播报平台、音量和语速沿用前置设置。
- 页面跟随手机系统深浅色主题。
- 二维码、实际连接地址、连接状态和刷新按钮位于游戏原生模组设置页。
- 微信、QQ 内打开时提示使用浏览器，并直接展示可复制的连接链接。

## 源码结构

| 路径 | 内容 |
| --- | --- |
| `mod/` | 游戏对接、局域网服务、原生设置页及手机端联动代码 |
| `preview/` | 收款页面模板、样式、动效和预览素材 |
| `lib/` | 二维码依赖的许可证 |
| `build.ps1` | 使用本机游戏程序集构建与打包 |
| `prepare-workshop.ps1` | 准备工坊目录、封面和 SteamCMD 上传配置 |
| `workshop-description.txt` | 可直接粘贴到创意工坊简介中的 Steam BBCode 文案 |

## 从源码构建

需要 Windows、.NET SDK 和本机安装的《Probably Stolen》。在项目目录执行以下命令。

```powershell
.\build.ps1
```

默认游戏目录为 `D:\Steam\steamapps\common\Probably Stolen`。也可以指定安装位置。

```powershell
.\build.ps1 -GameDir '你的游戏安装目录'
```

首次构建会从 NuGet 下载 Net.Codecrete.QrCodeGenerator 2.0.6。构建使用游戏自带的 Unity、Harmony 和游戏程序集，生成 `dist/MobileCasher.zip`。

手机页面随 DLL 内嵌，构建生成独立工坊目录 `dist/workshop/MobileCasher` 和本地安装包。将安装包内的 `Mods` 放入游戏根目录，在「改装件」中启用前置与本模组并重启。手机与电脑连接同一局域网，在本模组设置页扫码，用手机浏览器点击「开始收款」并保持页面在前台。

准备创意工坊发布时，指定封面图片。

```powershell
.\prepare-workshop.ps1 -CoverFile '封面图片路径'
```

脚本生成封面 JPEG 和 `dist/mobilecasher.vdf`，首次上传的可见性默认设为隐藏。SteamCMD 会将创建的物品 ID 写回该 VDF，后续构建保留这个 ID，以便更新同一物品。

## 权限与安全

当前声明两项能力。

- **network** 用于启动局域网 HTTP 服务，向手机提供页面、语音和游戏到账数据。
- **reflection-external** 用于对接已加载前置模组的设置、数字语音序列及播报入口。

网页、样式、动效和字体随 DLL 内嵌，前置 MP3 通过游戏提供的受限读取接口加载，因此当前实现已移除直接文件访问能力。

这些说明对应当前代码的用途，游戏授予的权限范围更广。

当前服务通过 HTTP 明文传输，尚未加入配对鉴权。能够访问该端口的其他设备也可以读取游戏收款记录。连接数量上限和请求总时限仍待完善，建议在可信局域网使用。

## 反馈

欢迎通过 Issues 反馈。请提供游戏版本、前置模组版本、手机系统和浏览器，以及出现问题时的操作与报错信息。

本模组完全通过 AI 许愿制作。收款界面和金额为游戏模拟内容。

## 致谢

- 到账语音及金额语音序列来自「到账语音播报 / Announce the Transaction」。
- 二维码生成使用 Net.Codecrete.QrCodeGenerator 2.0.6，采用 MIT 许可证。[许可证](lib/QrCodeGenerator-LICENSE.txt)
- 微信转账字体使用 Chiron GoRound。[字体许可证](preview/assets/Chiron-GoRound-LICENSE.txt)
