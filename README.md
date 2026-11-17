# New7z

[![Release](https://img.shields.io/github/v/release/outsideoftime/new-7z?label=release)](https://github.com/outsideoftime/new-7z/releases/latest) [![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE) [![Platform](https://img.shields.io/badge/platform-Windows%2010%20%2F%2011%20x64-0078D4)](#)

**New7z（新 7z）** 是一款面向 Windows 10 / 11 的开源免费压缩 / 解压软件，使用 WinUI 3 界面和完整的 7-Zip 引擎。支持创建 7z、ZIP、TAR，读取和解压 7z、ZIP、RAR、TAR、GZ、XZ、BZ2、ISO 等常见压缩格式；中文/英文界面、本地离线运行，不需要另行安装 7-Zip 或 .NET 运行库。当前正式版：**1.0.9**，Windows 10 / 11 x64。

[下载安装包](https://github.com/outsideoftime/new-7z/releases/latest) · [操作说明](docs/操作说明.md) · [MIT 许可](LICENSE) · [第三方许可](THIRD-PARTY-NOTICES.md) · [安装与分发说明](docs/安装包发布.md)

## 使用

下载安装 EXE，按向导安装到当前用户目录。安装包约 **30.16 MiB**，包含 .NET、WinUI 和自编译 7-Zip 引擎，可离线运行，无需安装完整 7-Zip。当前发行文件尚未进行发布者代码签名。详细操作见 [操作说明](docs/操作说明.md)。

Windows 10 x64（1809 及以上）请下载同一 Release 中的 `New7z-<版本>-win10-x64-Setup.exe`。它与 Windows 11 安装包使用相同的程序文件和版本号，只放宽了系统要求；Windows 10 上自动使用兼容右键菜单，Windows 11 安装包和现代菜单保持不变。

Windows 11 ARM64 提供 [1.0.10 ARM64 预览安装包](https://github.com/outsideoftime/new-7z/releases/tag/v1.0.10-arm64-preview)，已通过原生 ARM64 云端的功能、安装、升级与卸载测试，详情见 [ARM64 说明](docs/ARM64.md)。当前不支持 Windows 10 ARM64。

- 创建 7z、ZIP、TAR，7z/ZIP 支持密码与 7z 文件名加密；读取、解压和校验引擎支持的归档。设置中可选择默认格式，并分别指定 7z/ZIP 的默认压缩方法。
- 压缩包内直接预览图片和文本/代码文件，常见源码、配置、脚本扩展名（.cs、.py、.rs、.toml、Makefile 等）都可当作文本查看；支持中文文件名、文件夹、多选解压。
- 选中条目后可直接拖到资源管理器完成拖动解压；7z 和 ZIP 支持在包内添加文件、删除条目，写入采用临时文件校验后替换，失败或取消不影响原包。
- 自动更新按设置间隔检查并下载新版本，默认每天，可选 15 分钟、1 小时、6 小时或每周；启动及运行期间均生效，多个窗口共用检查时间，也可随时手动检查。GitHub 不可用或超时时回退至 Gitee；安装包经 SHA-256 校验并确认后安装，任务运行期间阻止安装和卸载。
- 主页支持批量解压，一次最多 200 包，每包独立排队并保留最近一批结果；失败或取消的主窗口任务可按原参数重试。任务中心按引擎字节进度显示速度和预计剩余时间。
- 资源管理器右键压缩/解压仅打开独立任务小窗口，不显示系统标题栏；成功后可手动或自动关闭，默认手动。
- 设置中配置右键菜单总开关与八项命令，也可修复菜单注册。
- 默认注册到 Windows「默认应用」及 7z、ZIP、RAR、TAR、GZ、XZ、BZ2、ISO 的「打开方式」；设置 → 文件关联 → **选择默认应用**，由用户选择默认格式。
- 应用支持浅色、深色、跟随系统，以及 PNG 图标替换。独立安装版中，7z、ZIP、RAR、TAR、GZ、BZ2、XZ、ISO 的单格式自定义图标会同步到资源管理器，优先于图标主题；恢复默认回到当前主题。对应格式需以 New7z 为默认程序。品牌图标替换仍只影响应用内。

勾选菜单任务后，资源管理器右键会出现 **New7z** 子菜单：Windows 11 在系统具备受信任身份包或现有开发注册条件时使用现代菜单，否则退回兼容菜单；Windows 10 始终使用兼容菜单。两种模式只注册一种，不会重复显示。不会自动开启开发者模式、导入证书、隐藏 Windows 自带压缩菜单或强制覆盖默认应用。

普通未加密 ZIP 的标准/更快模式使用流式 ZIP 编码；体积优先、密码 ZIP 和全部 7z 使用 7-Zip。读取、预览、解压仍使用 7-Zip。详见 [ZIP 性能记录](docs/ZIP性能优化.md)。

解压采用一次打开归档、批量输出，避免多文件包反复解析和固实块重复解码；保留路径预检、逐文件校验提交、冲突处理和取消清理。详见 [解压性能记录](docs/解压性能优化.md)。

本地开发分支已加入独立引擎进程、MotW 传播和解压预算、文件列表排序/属性、Zstandard/LZ4 单文件流、42 种文件图标与列表/任务刷新优化，均已随 1.0.2 发布。1.0.3 起右键「智能解压」替代「解压到同名文件夹」：包内只有一个顶层文件或文件夹时直接解压到当前目录，有多个顶层项目时解压到同名文件夹，多选时逐包判断；文件冲突默认保留两者，源包保留。4 GiB 以上 ZIP64 与 50,000 文件 ZIP/7z 压力样本已通过；正式签名仍待证书。实现与验证边界见 [稳定性与安全加固](docs/稳定性与安全加固.md)。

## 文件图标

新版的格式图标：7z、ZIP、RAR、TAR、GZ、BZ2、XZ、ISO 各有独立图标，用于文件关联、「默认应用」和「最近使用」列表。

| 7z | ZIP | RAR | TAR | GZ | BZ2 | XZ | ISO |
|:--:|:--:|:--:|:--:|:--:|:--:|:--:|:--:|
| ![7z](src/New7z.App/Assets/Formats/7z.png) | ![ZIP](src/New7z.App/Assets/Formats/zip.png) | ![RAR](src/New7z.App/Assets/Formats/rar.png) | ![TAR](src/New7z.App/Assets/Formats/tar.png) | ![GZ](src/New7z.App/Assets/Formats/gz.png) | ![BZ2](src/New7z.App/Assets/Formats/bz2.png) | ![XZ](src/New7z.App/Assets/Formats/xz.png) | ![ISO](src/New7z.App/Assets/Formats/iso.png) |

设置 → 外观与图标 → 压缩文件图标 → **图标主题** 还可以切换到内置的「动物图标」。独立安装版的主题和单个格式自定义 PNG 都会同步到 New7z 的文件关联图标，自定义优先于主题；资源管理器只有在对应格式的默认程序是 New7z 时才显示，可在设置 → 文件关联 → 选择默认应用中调整。

| 7z | ZIP | RAR | TAR | GZ | BZ2 | XZ | ISO |
|:--:|:--:|:--:|:--:|:--:|:--:|:--:|:--:|
| ![7z](src/New7z.App/Assets/Formats/Animals/7z.png) | ![ZIP](src/New7z.App/Assets/Formats/Animals/zip.png) | ![RAR](src/New7z.App/Assets/Formats/Animals/rar.png) | ![TAR](src/New7z.App/Assets/Formats/Animals/tar.png) | ![GZ](src/New7z.App/Assets/Formats/Animals/gz.png) | ![BZ2](src/New7z.App/Assets/Formats/Animals/bz2.png) | ![XZ](src/New7z.App/Assets/Formats/Animals/xz.png) | ![ISO](src/New7z.App/Assets/Formats/Animals/iso.png) |

也可以使用第三方图标合集手动替换单个格式。变形金刚主题合集见 [第三方图标合集](docs/第三方图标合集.md)；该图片版权归 Hasbro / Takara Tomy 等原权利人所有，不属于 New7z 的 MIT 许可，也不随安装包分发。

## 常见问题

**Windows 11 自带的压缩功能不支持 7z 或 RAR，怎么办？**
安装 New7z 后，右键文件或文件夹 →「显示更多选项 → New7z」即可压缩或解压；也可以在设置里把 7z、ZIP、RAR 等格式的默认应用设为 New7z。

**电脑没有安装 7-Zip，可以打开 .7z、.rar 文件吗？**
可以。安装包内含完整的 7-Zip 引擎和运行库，离线运行，不需要另装 7-Zip，也不需要 .NET 运行库。

**RAR 文件可以压缩吗？**
RAR 只支持读取和解压，不能创建；压缩请选择 7z、ZIP 或 TAR。

**需要管理员权限或联网吗？**
安装和使用压缩、解压、预览都不需要联网，也不需要管理员权限。应用更新会访问 GitHub Releases 检查并下载新版本，可以在设置 → 应用更新中关闭。

**和 Windows 自带的「压缩到」有什么区别？**
Windows 11 自带的压缩只支持 ZIP；New7z 支持 7z/ZIP/TAR 压缩，能读取更多格式，并提供压缩包预览、资源管理器右键菜单和文件关联。

**如何卸载？**
在 Windows「已安装的应用」中找到 New7z 卸载；卸载会恢复 New7z 修改过的系统「压缩到」菜单设置。

## English

New7z is an open-source Windows 10 / 11 archiver built with WinUI 3 and the full 7-Zip engine. It creates 7z, ZIP and TAR archives, extracts 7z, ZIP, RAR, TAR, GZ, XZ, BZ2 and ISO, previews images, text and source files inside archives, and integrates with the Explorer context menu and Windows default apps. Windows 10 x64 (1809 or later) uses the separate `New7z-<version>-win10-x64-Setup.exe` with the compatible context menu; both installers share the same program files. It runs offline and does not require a separate 7-Zip or .NET runtime installation.

## 构建

需要 Windows 11 x64、.NET SDK **10.0.403**、Visual Studio C++ x64 工具链和 Windows SDK。使用 PowerShell 7。引擎完整源码归档已包含在仓库，构建脚本自动解压并编译，不需要下载完整 7-Zip 安装程序。

```powershell
# 自包含开发版，输出 artifacts/app
./scripts/build.ps1
# 下载并验证 Inno Setup 6.7.4 编译器，仅用于打包
./scripts/get-build-tools.ps1
# 精简自包含安装包，输出 artifacts/releases（同时生成 Windows 10 兼容安装器）
./scripts/build-installer.ps1
# Microsoft Store 上传包，输出 artifacts/msix（需先填入 Partner Center 标识）
./scripts/build-msix.ps1 -RequireStoreIdentity
```

发行构建采用 WinUI 组件依赖与部分托管裁剪，保留自有程序集、JSON 源生成上下文和 WinRT.Runtime 全部成员（CsWinRT 的 ABI 辅助类型依赖反射解析，裁掉后会让 XAML 绑定对象编组失败）；可用 `-Untrimmed` 构建未裁剪对照版。Microsoft.Windows.SDK.NET 程序集会报告 IL2104 裁剪警告；已验证发布版设置、XAML 文件名绑定、文本/图片读取及快捷任务，更多环境仍需验证。

```powershell
./scripts/test.ps1                   # 引擎/安全/配置检查（含工作进程、链接、MotW）
./scripts/test-stress.ps1            # 可配置的大文件/海量文件压力测试
./scripts/test-drag-extraction.ps1   # 原生拖放数据与真实 Windows Shell 复制检查
./scripts/test-shell-preferences.ps1 # 9 项真实 COM 菜单配置检查
./scripts/test-installer.ps1         # 隔离产品标识的安装、修复、关联、升级、卸载检查
# 可通过 -AppDirectory 指定发布目录
./scripts/test-shell.ps1
./scripts/test-popup-close.ps1
```

安装测试临时替换当前用户的 New7z 系统集成，结束后恢复原注册；不覆盖真实产品的卸载标识。不要与实际压缩任务并行运行。GitHub Actions 自动构建安装器并检查引擎与原生菜单。

## 设计和实现范围

[工程说明](docs/工程说明.md) · [功能设计](docs/功能设计.md) · [交互原型](prototype/index.html) · [右键菜单设计](prototype/context-menu-design.html) · [DESIGN.md](DESIGN.md) · [UX-CONTRACT.md](UX-CONTRACT.md)

HTML 原型使用示例数据，不读写真实压缩包；真实产品在 `src/` 与 `native/`。原型检查：`npm ci`、`npm run check`、`npm run verify`、`npm run verify:menus`；浏览器缺失时先执行 `npx playwright install chromium`。

当前仅 x64，尚无跨卷回调。开发分支已实现独立引擎崩溃隔离、列表排序与属性，正式发行版范围与开发改动分别记录。RAR 已使用本地工程样本验证批量解压，但仓库尚未附带固定的 RAR 回归样本。具体预览大小限制与差异见工程说明。

自有代码采用 MIT；7-Zip 等组件维持原许可。发行包的 `Licenses` 附第三方通知、7-Zip 完整原始源码和构建适配代码。个人测试源文件、性能压缩包、证书和本地构建缓存不会提交到仓库。
