# 雀魂直播控制中心

Windows WPF 控制台与 OBS 浏览器源，当前为 0.292 调优测试版。

本仓库在建立版本管理时，根据已保存的源码快照和文件哈希恢复 0.292 各次迭代。提交日期为整理日期，未伪造历史提交时间；标签用于定位对应源码状态。更早的 v24～v29.1 尚未整理，不包含在这次历史中。

## 项目

- MajsoulBroadcastScorer-v29.2：WPF 界面与 OBS 服务。
- MajsoulBroadcastScorer.Core-v29.2：状态、积分与报告逻辑。
- MajsoulBroadcastScorer.Tests-v29.2：核心测试。
- MajsoulBroadcastBackend-v29.2：后台控制桥与事件测试。后台完整运行时为外部依赖，不随源码提交。

需要 Windows、.NET 8 SDK 及系统 WebView2。项目文件保留原目录名以兼容引用；显示版本为 0.292。

## 记录方式

每次迭代单独提交，说明改了什么、验证了什么；可交付版本加标签。账号配置、密钥、日志、历史对局和编译产物不提交。
