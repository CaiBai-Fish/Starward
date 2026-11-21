# Starward Fork 说明

本仓库是 [Scighost/Starward](https://github.com/Scighost/Starward) 的个人维护分支，仓库地址为 [CaiBai-Fish/Starward](https://github.com/CaiBai-Fish/Starward)。上游的米哈游游戏支持、更新机制与许可证保持原样；本分支在此基础上增加鹰角网络与库洛游戏的部分接入，并调整若干启动器行为与界面。

上游的原始 README（安装要求、本地化、开发与致谢）保留在 [README.upstream.md](./README.upstream.md)。

本分支与游戏官方无关联。第三方启动器、非官方接口以及终末地的启动方式均存在风险，使用前请阅读[已知限制与风险](#已知限制与风险)。

## 上游基线

| 项目 | 内容 |
| --- | --- |
| 上游仓库 | [Scighost/Starward](https://github.com/Scighost/Starward) |
| 基线提交 | `df8ae07b`（`filecache powered by fusioncache`，上游 0.18.2 之后） |
| 本分支提交 | `de188a6f` … `9a34f349`（完整列表见[功能提交记录](#功能提交记录)） |
| 许可证 | MIT，版权归 Scighost 及贡献者所有 |

上游 `main` 在 2026-11-18 仍停留在 `df8ae07b`。本文档描述的功能均已提交并推送到 [CaiBai-Fish/Starward](https://github.com/CaiBai-Fish/Starward)。

## 相对于上游的主要修改

### 1. 鹰角网络游戏接入

新增游戏标识：

| 游戏 | 区服 | GameBiz |
| --- | --- | --- |
| 明日方舟 | 国服 | `arknights_cn` |
| 明日方舟 | 国际服（Yostar） | `arknights_global` |
| 明日方舟：终末地 | 国服 | `endfield_cn` |
| 明日方舟：终末地 | 国际服（GRYPHLINK） | `endfield_global` |

#### 1.1 元数据与官方接口

- `Starward.Core/Hypergryph` 新增启动器客户端与数据模型，调用 `launcher.hypergryph.com` 与 `launcher.gryphline.com` 的 `batch_proxy`、`web/batch_proxy` 接口，获取版本、完整包、补丁包、预下载包、公告、横幅与主背景。
- 游戏信息、背景、内容、配置、包体等查询在 `HoYoPlayService` 中按游戏分流，鹰角游戏不经过 HoYoPlay 接口。
- `HypergryphGameMetadata` 将鹰角数据转换为上游通用的 `GameInfo`、`GamePackage`、`GameContent` 等模型，复用启动页、安装页与设置页。
- 加入图标与背景资源：`background_arknights.jpg`、`background_endfield.jpg`、`icon_arknights.png`、`icon_endfield.png`。

#### 1.2 安装、更新、预下载与修复

`Starward.RPC/GameInstall/HypergryphGameInstallService` 实现独立的安装流程：

- 完整包下载：支持多分片、断点续传、MD5 校验与解压。
- 增量更新：优先使用官方补丁，支持旧式补丁包与 V2 补丁清单（`V2PatchInfo`、`game_files`、`V2VerifyFiles`），补丁不可用时回退到完整包。
- 预下载：从 `PrePatch` 获取分片，写入安装目录下的下载缓存，并记录补丁指纹；能够识别官方启动器已有的 `launcher_tmp/preload` 或 `launcher_tmp/predownload` 缓存。
- 修复：校验本地文件清单，文件完整且版本最新时跳过下载，否则重新获取完整包。
- 硬链接：复用上游的硬链接机制，减少同一磁盘上的重复占用。
- 安装元数据：在游戏目录写入版本、补丁指纹等信息；官方启动器写入的版本文件无法读取时按 `0.0.0` 处理，避免安装流程中断。
- 安装路径：按发行商与游戏目录命名（如 `Hypergryph/Endfield Game`、`GRYPHLINK/Arknights Endfield`），目录名与官方客户端保持一致。
- 国际服（Yostar）：明日方舟国际服由 `Starward.RPC/GameInstall/YostarArknightsGameInstallService` 负责，版本清单、CRC64 文件校验与下载走 Yostar 官方接口。
- 安装、更新、预下载与修复均接入上游的 `GameInstallOperation` 流程与进度上报。

#### 1.3 启动

- 明日方舟：按普通可执行文件启动，不附加米哈游专属参数。
- 终末地（国服与国际服）：游戏会校验直接父进程是否为官方签名程序，直接启动会被拒绝。本分支增加 `--endfield-launch-worker` 辅助进程：
  - 以管理员权限重新启动 Starward 自身作为 worker（UAC 提权）；
  - 在游戏目录中查找官方签名候选文件，复制为临时 `Games.exe`，校验数字签名后以挂起状态作为父进程；
  - 通过 `PROC_THREAD_ATTRIBUTE_PARENT_PROCESS` 创建 `Endfield.exe`；
  - 失败时回退为直接启动；启动日志写入 `%TEMP%\StarwardEndfieldLaunch.log`。
- 相关实现：`EndfieldLaunchWorker`、`GameDecoyParentBridge`、`OfficialLauncherTrust`、`ParentProcessLauncher`。
- 启动参数：终末地提供 DX11 选项（`-force-d3d11`）；`-popupwindow`、DX12、`login_auth_ticket` 与 CMD 启动等米哈游专属逻辑不会应用到第三方游戏。
- 游戏运行状态与游玩时长继续由上游的 `RunningGameService` 与 `PlayTimeRecordService` 记录。

#### 1.4 账号与寻访记录（终末地国服）

- 鹰角账号登录：通过登录 token 获取账号与角色信息；token 使用 Windows DPAPI（`CryptProtectData` / `CryptUnprotectData`）加密后存入 SQLite，不以明文落盘。
- 寻访记录：支持角色寻访、武器寻访与基础寻访三个卡池，支持从游戏日志导入、按账号增量同步，以及各卡池的数量、六星/五星/四星占比与平均六星抽数统计。
- 账号记录：同步理智、日常等账号数据，提供独立的账号记录页。
- 每日便签：在启动页右侧的日常便签中显示终末地的日常状态；未登录鹰角账号时给出提示。
- 数据库：新增 `EndfieldGachaAccount`、`EndfieldGachaItem`、`EndfieldGachaAuth`、`EndfieldGachaInfo`、`EndfieldAccountRecord`、`EndfieldAccountRecordSync` 表，数据库版本推进到 v25。
- 账号切换：启动器内的账号切换仅选择寻访记录与日常数据所使用的鹰角账号，不修改游戏登录状态。

#### 1.5 明日方舟记录页与日常便签

- 国服与国际服共用一套记录页，数据来自森空岛（Skland）与鹰角账号接口。
- 角色信息：昵称、UID、等级、主线进度、干员数、皮肤数与头像。
- 分类记录：剿灭作战、尖灭作战、集成战略、活动战绩、连战、沙盒与卫戍协议。
- 账号记录与设备指纹：复用米哈游工具箱的账号与设备指纹布局，支持多账号与角色下拉切换。
- 日常便签（国服）：在启动页右侧显示明日方舟的日常状态；国际服暂不提供便签。
- 国际服的安装、更新与启动由 `YostarArknightsGameInstallService` 与 `YostarGameClient` 负责，版本清单与文件校验使用 Yostar 官方接口。

#### 1.6 Skland 工具箱与游戏数据 H5

- 明日方舟与终末地的记录页均提供森空岛游戏数据 H5 入口，使用 WebView2 在应用内打开。
- H5 窗口复用米哈游工具箱的登录与设备指纹实现，窗口语言跟随应用。
- 游戏数据由森空岛站点自身提供，本分支仅提供内嵌浏览器与入口。

#### 1.7 终末地工具箱

- 终末地国服与国际服新增"终末地工具箱"页面，替代原先直接打开的 H5 记录入口；H5 仍作为记录入口保留。
- 战绩：角色与卡牌详情、挑战关卡与队伍编成。
- 账号记录：理智、日常等账号数据，支持多账号与角色下拉切换。
- 危机合约：赛季战绩、指标总览、合约指标、阶段任务、行动规则与敌人情报。
- 战争回响：赛季、星级、评价、荣誉、成就与周期评级。
- 影拓丰碑：赛季战绩、机制特权、敌方情报与通关记录。
- 数据来自官方接口与森空岛，均非公开协议。

#### 1.8 终末地规划器

- 启动页右侧新增"终末地规划器"入口，使用 WebView2 内嵌加载 CEP（`https://end.canmoe.com`）。
- 支持加载失败重试与在外部浏览器打开；窗口语言跟随应用。
- CEP 为第三方社区站点，账号与云同步均由站点自身处理，本分支仅提供内嵌浏览器窗口。

### 2. 库洛《鸣潮》接入

- 新增 `kirara_cn`、`kirara_global` 标识与注册表路径，用于识别国服/国际服客户端。
- `ThirdPartyGameService` 在常见安装目录与注册表中查找 `Wuthering Waves.exe`，自动写入安装路径。
- 游戏选择器将已安装的第三方游戏固定到顶部图标区；网络请求失败时也能从本地扫描结果恢复显示。
- 版本更新与安装由库洛官方启动器负责；启动页按钮打开官方启动器，而非直接启动游戏。
- 不提供安装、更新、抽卡记录与日常功能。

### 3. 截图画廊视频播放

- 图库扫描与文件监控加入 `.mp4`。
- 视频缩略图显示播放标识，不再通过图像解码器读取尺寸。
- 详情窗口使用 `MediaPlayer` 与 `MediaPlayerElement` 播放视频，支持空格暂停/继续，显示分辨率与文件大小。
- 视频模式下隐藏缩放、编辑、复制为 JPG 等图片操作；删除视频前先停止播放并释放文件句柄，删除失败时恢复播放。
- 背景视频的暂停/继续消息处理同步调整，避免切换背景时重建播放器。

### 4. 启动器体验

- 开机自启：写入 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`，支持静默启动到系统托盘（`--hide`）；程序路径变化时自动修复注册表项。
- 游戏退出后显示启动器：进程退出时发送 `GameExitedMessage`，按设置显示主窗口并回到游戏启动页（2 秒节流，避免重复弹窗）。
- 游玩时长：游戏退出后轮询数据库并刷新显示；主窗口重新获得焦点时刷新（2 秒节流）。
- "游戏启动后"操作选项从游戏启动设置对话框移动到通用设置。
- 游戏选择器由横向 `StackPanel` 改为 `ItemsRepeater` 与 `UniformGridLayout` 组成的自适应网格，便于容纳更多游戏。
- 终末地分辨率设置接入注册表键，支持读取与写入全屏/窗口分辨率。
- 战绩窗口改为单例，可从任意页面打开；账号验证提示可跳转到对应角色。

### 5. 稳定性与兼容性修复

- 游戏列表：各启动器渠道逐个请求，单渠道失败不再中断整个列表；请求失败重试 3 次（3 秒、6 秒退避），仍失败时回退到上次缓存；空列表不写入缓存。
- HTTP：HoYoPlay 客户端连接超时 15 秒、总超时 30 秒；其他 HTTP 客户端连接超时 15 秒，减少代理环境下的长时间挂起。
- Cookie：按第一个 `=` 切分，修复 `cookie_token_v2`、`ltoken_v2` 等值中包含 `=` 时被丢弃的问题。
- 游戏账号：登录凭据刷新导致 SHA256 变化时，按 UID 认领已保存账号，避免出现重复账号或当前账号变成未保存项；注册表 UID 末尾的 `\0` 正确截断。
- 数据库：兼容旧库缺失 `ZZZDeadlyAssaultInfo` 列的情况；鹰角建表语句可重复执行，避免升级路径分叉导致建表失败。
- 主界面：消息订阅在 `Loaded` 与 `Unloaded` 中注册与释放，避免重复订阅。
- 第三方游戏没有 HoYoPlay 包体，或游戏信息为空时，相关页面跳过请求，避免空引用异常。

## 功能提交记录

| 提交 | 说明 |
| --- | --- |
| `de188a6f` | 鹰角/库洛游戏接入的阶段性提交：游戏枚举、鹰角客户端与元数据、安装与启动基础、开机自启、游戏退出后打开启动器、设置调整 |
| `131960f6` | 截图画廊支持 MP4 播放 |
| `de925ba9` | 终末地完整接入：安装服务、启动辅助进程、寻访记录、账号记录、日常便签、数据库与界面 |
| `a458e21c` | 鹰角游戏渠道图标与公告客户端 |
| `f2dc56ee` | 明日方舟国际服（Yostar）接入：GameBiz、Yostar 客户端、CRC64 校验与安装服务 |
| `6a8a4bda` | 明日方舟国际服下载在弱网下的重试与校验加固 |
| `20c04e29` | 补全明日方舟国际服的安装与启动校验 |
| `5381b760` | 明日方舟国际服启动器背景与资讯 |
| `cc95ceae` | 明日方舟国服日常便签 |
| `0dbc52cc` | 明日方舟记录页与主界面入口 |
| `dd4ad6fa` | 按 Starward 视觉重做明日方舟记录页 |
| `22b6edf4` | 明日方舟记录页对齐米哈游工具箱布局 |
| `e17d89f7` | 明日方舟记录分类图标与 H5 入口 |
| `f06d2f72` | 森空岛游戏数据 H5 应用内窗口 |
| `5d8d28d6` | 复用米哈游工具箱的登录与设备指纹布局 |
| `7eb47beb` | 终末地工具箱从明日方舟记录页拆分 |
| `9c949f72` | 森空岛工具箱同步到终末地与国服构建 |
| `a2ec9a57` | 终末地工具箱采用 HoYoLAB 工具箱布局 |
| `203993d5` | 终末地工具箱菜单项与 HoYoLAB 一致 |
| `f4de8d39` | 终末地账号操作移入角色下拉菜单 |
| `736cdef6` | 明日方舟工具箱角色下拉布局同步 |
| `2745b0a8` | 多明日方舟账号存储与角色下拉同步 |
| `de46d4fe` | 终末地记录改为原生加载，H5 保留为记录入口 |
| `8502e614` | 终末地挑战关卡与队伍编成 |
| `9a34f349` | 终末地危机合约、影拓丰碑与战争回响页面 |

提交信息中的 `wip` 为开发过程中的临时命名，功能范围以本文档为准。

## 构建

与上游一致：

- Windows 10 1809（17763）及以上。
- Visual Studio 2022，安装 .NET 桌面开发、使用 C++ 的桌面开发、通用 Windows 平台开发工作负载。
- .NET SDK 10.0.400（见 `global.json`，允许向上滚动）。
- WebView2 Runtime（终末地规划器、网页登录等使用）。

使用 Visual Studio 打开 `Starward.slnx` 构建；命令行可参考 `build.ps1`，脚本中的 MSBuild 路径需按本机安装位置调整。

## 与上游同步

建议保留两个远程：

```powershell
git remote add origin https://github.com/Scighost/Starward.git
git remote add fork https://github.com/CaiBai-Fish/Starward.git
git fetch origin
git rebase origin/main
```

也可以使用 `git merge origin/main`。变基后推送个人分支通常需要 `git push --force-with-lease fork main`。上游改动集中在米哈游相关代码时，冲突一般出现在游戏枚举、安装分发、游戏选择器与数据库版本等公共位置。

## 已知限制与风险

- 鹰角启动器接口与终末地启动校验均非公开协议，官方更新后可能失效；本分支不保证长期可用。
- 终末地启动需要管理员权限，每次启动会触发 UAC。伪装父进程的做法参考 Xel-Launcher 与 EndfieldNoLauncher 的实现思路，可能被游戏或反作弊判定为异常，请在了解风险后使用。
- 应用内更新检查仍指向 `Scighost/Starward` 的发布页面，fork 构建可能收到上游版本更新提示，并可能被替换为上游版本。如不希望失去本分支的功能，请关闭自动更新或自行调整更新源。
- 终末地国际服支持安装、更新、启动、设置、截图与终末地工具箱，不包含寻访记录与日常。
- 明日方舟国服支持安装、更新、启动、设置、截图、记录页与日常便签；国际服支持安装、更新、启动、截图与记录页，不提供便签与游戏设置页。
- 明日方舟记录与终末地工具箱的数据来自森空岛与官方接口，均非公开协议；登录 token 过期后需重新登录。
- Starward 不实现"免登录"：官方启动器不会通过命令行把登录态传给游戏，游戏免登录依赖自身的 SDK 登录缓存。首次进入游戏仍需在游戏内登录一次，之后由游戏自身缓存免登录。
- 鸣潮的安装、更新、抽卡记录与日常由官方启动器负责，本分支仅做识别与启动。
- 第三方游戏不应用米哈游专属启动参数、账号切换与免登录逻辑。
- 以上功能仅在 Windows 上验证，未对其他平台或上游后续版本作兼容性承诺。

## 许可证

本项目沿用上游的 MIT 许可证，原始版权归 Scighost 及贡献者所有，新增代码同样以 MIT 许可证发布。使用、分发或修改时请保留原始版权声明。
