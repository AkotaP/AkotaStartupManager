# Changelog

本项目的重要变化记录在此文件中。

格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，版本遵循 [Semantic Versioning](https://semver.org/lang/zh-CN/)。

## [Unreleased]

### Planned

- 持续改进可访问性、条件测试体验与 Windows 集成测试。

## [0.2.0] - 2026-09-23

### Fixed

- 修复注册表启动项的值类型为 `REG_BINARY`、`REG_MULTI_SZ` 等非字符串时，禁用后无法恢复、原始数据永久丢失的问题；现在按值类型完整保存，并在恢复时还原。
- 修复移动便携目录后，启动文件夹的备份因记录的是绝对路径而全部失效的问题；备份位置改为相对程序目录记录，并保留按原始文件名找回的兜底。
- 修复恢复系统级启动项时不检查管理员权限、只抛出底层异常的问题；恢复现在与禁用一样先做提权确认。
- 修复恢复一条已被“接管启动”接管的启动项时保留接管规则、导致同一程序在登录时可能被启动两次的问题；恢复时可一并删除关联规则。
- 修复禁用后的启动项只能恢复最近一条、其余备份在界面上完全无法访问的问题。

### Changed

- “Windows 启动项”页面的“恢复最近一项”改为“备份与恢复”窗口：可查看全部备份及其可恢复状态，选择任意一条还原，并丢弃不再需要的备份记录。
- 备份窗口会列出备份目录中没有对应记录的备份文件（例如配置文件损坏回退后留下的），可指定恢复到当前用户或公共启动文件夹。
- 删除“Windows 启动项”列表中始终勾选且不可交互的“启用”列；系统级启动项统一通过“禁用并备份”处理。

## [0.1.4] - 2026-09-21

### Fixed

- 修复“接管启动”列表中“启用”列的复选框无法点击、切换不生效的问题；现在可以直接勾选或取消勾选单条规则，状态会即时保存并立即应用于监控。

## [0.1.3] - 2026-07-22

### Fixed

- 修复在规则编辑器中输入条件参数时，条件树刷新导致当前条件失去选中、属性面板切回根节点的问题。

## [0.1.2] - 2026-07-22

### Added

- 支持通过“编辑规则”按钮或双击规则行修改已有接管规则。
- 中英文 README 增加项目完全由 AI 编写的说明。

### Changed

- 规则编辑采用深复制的事务式流程，取消或保存失败时不会污染原规则及当前监控配置。

## [0.1.1] - 2026-07-22

### Fixed

- 修复初始化完成后功能按钮仍保持不可点击，以及列表选择变化不刷新相关按钮状态的问题。
- 为不可用按钮增加明确的禁用外观，避免与界面遮挡混淆。

### Changed

- 使用根目录 `VERSION` 作为普通构建与发布脚本的统一版本来源，同时保留 `-Version` 临时覆盖能力。
- 发布脚本一次生成无需预装 .NET 的 standalone 包，以及依赖 .NET 10 Desktop Runtime 的轻量 runtime 包，并分别提供 SHA-256 文件。

## [0.1.0] - 2026-07-22

### Added

- 现代化简体中文 WPF 主界面和系统托盘菜单。
- 注册表 `Run` / `RunOnce`、启动文件夹和开机/登录计划任务发现与管理。
- 可恢复的启动项禁用和恢复记录。
- 将 Windows 原生启动项导入为接管规则。
- 支持嵌套 AND / OR 条件表达式树。
- Process、TcpPort、Http、FileExists、WindowsService 和 WindowTitle 条件。
- 连续成功阈值、就绪后延迟、防重复启动、存活验证和自动重试。
- 单实例唤醒、随 Windows 后台启动和按需 UAC 重启。
- 便携 JSON 配置、原子写入、损坏恢复和按日滚动日志。
- `win-x64` 自包含便携发布脚本和 SHA-256 产物。
- 中英文项目文档、GPL-3.0 许可证与 GitHub 社区模板。

[Unreleased]: https://github.com/AkotaP/AkotaStartupManager/compare/v0.2.0...HEAD
[0.2.0]: https://github.com/AkotaP/AkotaStartupManager/compare/v0.1.4...v0.2.0
[0.1.4]: https://github.com/AkotaP/AkotaStartupManager/compare/v0.1.3...v0.1.4
[0.1.3]: https://github.com/AkotaP/AkotaStartupManager/compare/v0.1.2...v0.1.3
[0.1.2]: https://github.com/AkotaP/AkotaStartupManager/compare/v0.1.1...v0.1.2
[0.1.1]: https://github.com/AkotaP/AkotaStartupManager/compare/v0.1.0...v0.1.1
[0.1.0]: https://github.com/AkotaP/AkotaStartupManager/releases/tag/v0.1.0
