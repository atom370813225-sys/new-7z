# Windows ARM64 预览版

目标系统：Windows 11 ARM64。主程序、独立引擎进程、7-Zip 引擎、原生桥接、Explorer 扩展及 Shell 启动器均采用 ARM64，.NET 与 WinUI 运行库随包提供。ARM64 安装器拒绝 x64 系统；当前不提供 Windows 10 ARM64 安装包。

## 构建

在具备 Visual Studio C++ ARM64 工具、Windows SDK 和 .NET SDK 的 Windows 环境中运行：

```powershell
./scripts/build-native.ps1 -Architecture arm64
./scripts/build-installer.ps1 -Architecture arm64
```

x64 Windows 使用 `x64_arm64` 工具链交叉编译；ARM64 Windows 优先使用 `Hostarm64` 编译器。源工程统一通过 `New7zArchitecture` 选择原生 DLL、RuntimeIdentifier 和引擎进程目录，避免跨架构混用。MSIX 构建也接受 `./scripts/build-msix.ps1 -Architecture arm64`。

安装器与清单输出到 `artifacts/releases/arm64`，运行载荷位置记录在 `artifacts/latest-installer-payload-arm64.txt`。ARM64 与 x64 的载荷、安装器清单和校验文件分别保存；正式发布多架构资产时，发布者需要合并各架构的 SHA-256 清单。

打包脚本逐一检查载荷内所有 PE 文件：仅允许 ARM64 二进制与中立的 AnyCPU 程序集，并检查 App / EngineHost 的运行目标和 Shell 身份包架构。运行时更新按当前进程架构选择安装器；缺少 ARM64 资产时不改选 x64 安装器。

## 没有设备时的验证

`.github/workflows/build.yml` 配置了 `windows-11-arm` 原生 GitHub-hosted runner，运行以下检查：

- 从源代码构建完整 ARM64 引擎、应用和安装器，检查载荷架构。
- 归档集成、密码与分卷、进程隔离、安全边界及更新包架构选择；测试启动时核对进程确实为 ARM64。
- PNG / ICO 图标、原生拖放和 Shell 菜单偏好。
- 隔离安装、关联修复、无外部 .NET SDK 时启动裁剪后的 WinUI 程序、原生界面诊断、升级和卸载。

云端自动检查可以验证 ARM64 程序实际执行；手动资源管理器右键点击、高 DPI、多显示器、触屏和具体设备性能仍需 ARM64 用户反馈。不能把 x64 上的交叉编译通过当成 ARM64 运行通过。

## 2026-11-17 验证与预览包

[原生 ARM64 / x64 Actions 运行记录](https://github.com/outsideoftime/new-7z/actions/runs/43991633717) 已全部通过，构建提交 `5b9786d44741cd1984614c1ec9180952b11562cd`。

| 检查 | ARM64 云端 | x64 云端 / 本机 |
|---|---|---|
| 完整应用、引擎、Shell 和安装器构建 | PASS | PASS |
| 核心归档、安全边界、更新架构选择 | 350 项 | 350 项 |
| PNG / ICO 图标 | 45 项 | 45 项 |
| 原生拖放与真实 Shell 放置目标 | 117 项 | 117 项 |
| 原生菜单偏好 | 16 项 | 16 项 |
| 安装、正常启动、裁剪版 WinUI、升级、卸载 | 73 项，原注册恢复 | 本轮使用 ARM64 云端验收 |

本机还完成了 ARM64 解决方案交叉编译，零警告、零错误；该编译结果与上面的原生 ARM64 运行结果分别记录。裁剪构建保留既有 Windows SDK / WinRT.Runtime 的 IL2104 警告，发布后的原生运行诊断通过。

下载：[GitHub ARM64 预览版](https://github.com/outsideoftime/new-7z/releases/tag/v1.0.10-arm64-preview) · [Gitee 镜像](https://gitee.com/wgliangzero/new-7z/releases/tag/v1.0.10-arm64-preview)。预览版不进入稳定版自动更新通道。

`New7z-1.0.10-arm64-Setup.exe` 为 **28.45 MiB**（29,831,558 字节），SHA256 `C5ED4E800389B4F3F626DB09B3835C913947591B4FF0E9846B414790B195BCA2`。运行载荷 104,678,833 字节、295 个文件，BuildId `3d4e6aa52763`；安装包未签名。

官方依据：[GitHub-hosted runners](https://docs.github.com/en/actions/reference/runners/github-hosted-runners)、[Inno Setup 架构限制](https://jrsoftware.org/ishelp/topic_setup_architecturesallowed.htm)。
