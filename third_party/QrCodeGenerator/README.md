# QR Code Generator 标准核心

来源为 Manuel Bleichenbacher 的 Net.Codecrete.QrCodeGenerator，固定使用上游 v2.0.6。[上游源码](https://github.com/manuelbl/QrCodeGenerator/tree/v2.0.6/QrCodeGenerator)

本目录保留以下上游文件，文件中的版权与许可证声明完整保留。

- BitArrayExtensions.cs
- DataTooLongException.cs
- Graphics.cs
- Objects.cs
- QrCode.cs
- QrSegment.cs
- ReedSolomonGenerator.cs

MobileCasher 使用标准 EncodeText 接口编码局域网网址。本目录仅采用标准核心，高级日文编码与最优分段扩展 QrSegmentAdvanced 未纳入构建。

build.ps1 从这些源码编译 QrCodeGenerator.dll。许可证见 LICENSE.txt。
