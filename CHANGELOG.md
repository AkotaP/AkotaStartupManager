# Changelog

本项目的重要变化记录在此文件中。

格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，版本遵循 [Semantic Versioning](https://semver.org/lang/zh-CN/)。

## [Unreleased]

### Planned

- 持续改进可访问性、条件测试体验与 Windows 集成测试。

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

[Unreleased]: https://github.com/AkotaP/AkotaStartupManager/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/AkotaP/AkotaStartupManager/releases/tag/v0.1.0
