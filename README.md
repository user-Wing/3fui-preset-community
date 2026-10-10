# 3FUI 预设社区

3FUI v6 的独立 WinForms 插件。页面沿用 VideoEnhancer 模型下载页的 LakeUI 控件、四列资源列表、分组、DPI 布局和三路并行下载调度。

1.0.1 已按 VideoEnhancer 对齐外层留白、页签、页内标题/按钮、列表与独立底部状态栏。全部控件先按 96 DPI 创建再统一缩放，避免高 DPI 下的拥挤和重叠。替换 DLL 后需重启 3FUI 生效。

资源来源：[ARXChem/Software-List 的预设目录](https://www.modelscope.cn/datasets/ARXChem/Software-List/tree/master/ffmpegfreeui/Preset)。每次刷新动态获取目录，目前为 9 个 v6 JSON 预设。

## 安装与使用

将 `PresetCommunity.3fui.dll` 放入 3FUI 主程序目录的 `plugin` 文件夹，重启后在侧栏选择“预设社区”。

- “刷新资源”：同步远端文件及本地安装状态。
- “下载全部”或“下载本组”：下载未安装或需要更新的预设，最多三项并行；批量期间按钮变为“停止下载”。
- 点击单项操作列可下载、更新、取消或重试。
- 已安装预设支持右键删除，删除前需要确认。
- 下载后打开参数面板 → 预设管理 → 从社区下载，选择并读取预设。

预设写入 `Preset_v6\Community`，保留原始 JSON；以远端 SHA-256 判断是否最新。文件大小、SHA-256 和 v6 格式全部校验成功后才安装，更新同名文件会保留 `.bak`，取消或失败时清理 `.partial`。用户自定义预设不受影响。

## 构建

需要 .NET 10 SDK，编译时还原 LakeUI 5.110.0；运行时使用 3FUI 自带的 LakeUI，不复制另一份 LakeUI DLL。

```powershell
dotnet build PresetCommunity.vbproj -c Release
dotnet build PresetCommunity.vbproj -c Release '-p:PluginInstallDir=C:\path\to\3FUI\plugin'
dotnet run --project tests\Smoke.vbproj -c Release -- '<测试输出目录>'
dotnet run --project tests\Layout.vbproj -c Release -- 96
dotnet run --project tests\Layout.vbproj -c Release -- 144
dotnet run --project tests\Layout.vbproj -c Release -- 192
```

构建产物位于 `bin\Release\net10.0-windows10.0.17763.0\PresetCommunity.3fui.dll`。安装命令中的路径需替换为自己的 3FUI 插件目录；布局测试在 Windows 上运行，网络 Smoke 测试会真实下载远端文件，请使用专门的测试输出目录。

源码中的 `ModelDownloadCoordinator.vb`、`LakeLayoutPanel.vb` 以及 `PresetPanel.Appearance.vb` 的相关控件设置来自 [VideoEnhancer](https://github.com/maxzrb/VideoEnhancer)（MIT，见 LICENSE），仅调整插件命名空间和预设所需部分。宿主 [FFmpegFreeUI](https://github.com/Lake1059/FFmpegFreeUI) 的入口契约为程序集名对应的 `PresetCommunity.Entry` 和 `SetHost_AddCustomWinformPanel` 回调。

版本记录见 [CHANGELOG.md](CHANGELOG.md)。本项目源码采用 [MIT 许可](LICENSE)，LakeUI 及宿主的授权条件以其各自许可为准。

布局测试使用隐藏的独立面板，检查控件相交、边界及缩放尺寸，不操作运行中的 3FUI。192 参数在当前设备 DPI 不为 192 时模拟相应缩放。
