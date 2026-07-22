# 参与贡献

感谢你愿意帮助改进 Akota Startup Manager。项目涉及注册表、计划任务、进程启动和权限提升；请把可恢复性和用户安全放在功能便利之前。

## 开发环境

- Windows 10/11 x64
- .NET 10 SDK
- Visual Studio 2026、Rider 或 VS Code（可选）
- PowerShell 5.1 或更高版本

```powershell
git clone https://github.com/AkotaP/AkotaStartupManager.git
Set-Location .\AkotaStartupManager
dotnet restore .\AkotaStartupManager.slnx
dotnet build .\AkotaStartupManager.slnx -c Release
dotnet test .\AkotaStartupManager.slnx -c Release --no-build
```

## 架构规则

- `Core` 只能包含领域模型和抽象，不依赖 Windows UI 或基础设施。
- `Application` 编排用例，只依赖 `Core`。
- `Infrastructure` 实现 Windows API、持久化和日志接口。
- `App` 负责 WPF、MVVM、托盘和依赖组装。
- UI code-behind 只处理纯视图交互；业务操作放入 ViewModel 或 Application 服务。
- 新增 Windows 写操作时必须提供失败反馈，并优先设计恢复或回滚路径。

## 提交改动

1. 从最新默认分支创建聚焦单一目标的分支。
2. 遵循仓库 `.editorconfig` 和周围代码风格。
3. 对业务规则和配置持久化改动补充测试。
4. 不要提交真实注册表导出、用户名路径、日志、配置、凭据、证书、`Data/` 或 `Logs/`。
5. 在 Pull Request 中说明用户影响、测试方式和权限/恢复行为。

建议使用清晰的提交前缀，例如：

```text
feat: add named-pipe readiness condition
fix: restore registry value kind correctly
docs: explain portable data directory
test: cover retry cancellation
```

## Pull Request 检查表

- [ ] Release 构建 0 错误、0 警告
- [ ] 全部测试通过
- [ ] Windows 写操作具有明确错误反馈和恢复策略
- [ ] 没有提交本机数据或敏感信息
- [ ] 中英文用户文档在需要时同步更新
- [ ] UI 改动已实际启动程序检查

## 报告问题

优先使用仓库 Issue 模板。请提供：

- Windows 版本与应用版本
- 启动项来源（注册表、启动文件夹或计划任务）
- 可重复步骤和期望结果
- 已脱敏的相关日志片段

安全漏洞不要创建公开 Issue，请遵循 [SECURITY.md](SECURITY.md)。

## 发布版本

维护者手工发布流程：

```powershell
# 在干净工作区执行；版本号自动读取仓库根目录 VERSION
.\scripts\publish.ps1

# -Version 仅用于不修改 VERSION 的临时或预发布覆盖
# .\scripts\publish.ps1 -Version 0.1.1-beta.1

# 检查 artifacts\ 下 standalone/runtime 两个 ZIP 及各自 SHA-256 后
# 创建并推送与 VERSION 一致的标签，再上传四个文件
$version = (Get-Content .\VERSION -Raw).Trim()
git tag -a "v$version" -m "Akota Startup Manager v$version"
git push origin "v$version"
```

发布前还应：

1. 更新根目录 `VERSION`，并在 `CHANGELOG.md` 中把目标版本从 Unreleased 转为发布日期。
2. 确认 standalone/runtime 两个包的程序集版本、归档文件名和 `VERSION` 一致。
3. 分别解压两个发行 ZIP；在普通用户权限下完成 standalone 冒烟测试，并在已安装 .NET 10 Desktop Runtime 的环境测试 runtime 包。
4. 确认两个 ZIP 均不包含 `Data/`、`Logs/`、测试输出或个人信息。
5. 将两个 ZIP 和对应的两个 `.sha256` 文件一起上传到 GitHub Release。

## 许可证

提交贡献即表示你有权提交这些内容，并同意其按仓库的 [GPL-3.0](LICENSE) 许可证分发。
