# 终末地（Arknights: Endfield）客户端账号缓存机制逆向分析

> 分析对象：本机 \`F:\\Endfield Game\`（国服，客户端 \`CN_WIN_REL_1.6.3_C10781849_E10704668\`）
> 分析日期：2026-11-23
> 方法：文件系统与注册表取证、Unity IL2CPP 元数据解析、SDK DLL 字符串与 x86-64 反汇编（capstone）、HGSDK/U8SDK 运行日志、一次通过 Starward 启动的运行时验证。

---

## 0. 结论摘要

终末地的“账号缓存”是**两层结构**：

1. **鹰角账号层（SDK 层）**：由 \`hgsdk.dll\`（HGSDK 1.28.0.10）通过 **MMKV 1.2.15** 维护 \`login_cache\` 实例，缓存鹰角通行证账号（hgId）、账号 token、remember_login、登录方式与最近登录账号等；文件位于
   \`%USERPROFILE%\\AppData\\LocalLow\\Hypergryph\\Endfield\\sdk_data_<hash>\\login_cache\`，
   数据区使用 **AES-CFB128** 加密，随机 IV 存放在同名 \`.crc\` 的 MMKV metaInfo 中。
   配套的 \`sdk_hgsdk_data_1\`（明文 MMKV）保存设备 GUID（deviceId），
   注册表 \`HKCU\\Software\\Hypergryph\\sdk_data\\<hash>\\REG_GUID\` 与 \`REG_GUID_V3_Encrypt\` 保存缓存主密钥（后者由 **Windows DPAPI** 保护）。

2. **游戏角色层（客户端层）**：由 \`Assembly-CSharp\`/\`Gameplay.Beyond\` 的
   \`Beyond.PlayerLocalData\`、\`Beyond.Gameplay.Core.ClientDataManager\` 维护，
   按“鹰角账号 UID + 游戏角色 UID”分目录缓存：
   - \`ClientData\\Local\\*.json\`（本机级设置与红点状态）
   - \`ClientData\\User\\<游戏UID>\\*.json\`（角色级 UI/状态缓存，例如 \`Default.json\`、\`Dungeon.json\`、\`SNS.json\`）
   - Unity PlayerPrefs（注册表 \`HKCU\\Software\\Hypergryph\\Endfield\`）中的
     \`u8sdk_cached_uid\`、\`PLDK_cachedUserId\`、\`PLDK_cachedRoleId\`

3. **自动登录**：客户端启动后由 HGSDK 读取 \`login_cache\` 中持久化的凭据，
   调用 \`as.hypergryph.com\` 的 OAuth/token 接口完成免密登录；
   本次运行验证中，13:25:41 在 **未输入任何凭据** 的情况下完成登录（LoginStatus 1→2），
   且 \`login_cache\` 文件时间戳未变，说明**自动登录路径只读缓存、不重写缓存**。

---

## 1. 分层架构

\`\`\`
Endfield.exe (Unity IL2CPP, GameAssembly.dll)
  └─ Assembly-CSharp / Gameplay.Beyond / Entry.Beyond
       ├─ Beyond.LoginManager（登录工作流）
       ├─ Beyond.PlayerLocalData（用户级键值缓存）
       └─ Beyond.Gameplay.Core.ClientDataManager（ClientData JSON 落盘）
  └─ U8SDK.dll (2.34.5)            渠道/平台层：U8ChannelSdkLogin、AutoLogin、LogOut、GetToken
       └─ HGU8SDK*.dll / HGP.dll
  └─ hgsdk.dll (HGSDK 1.28.0.10)   鹰角账号层：LoginImpl、QHgSdkData、UserInfoAssist
       ├─ MMKV 1.2.15（login_cache / mmkv.default / gameprotocol_cache / sdk_hgsdk_data_1）
       └─ Windows 注册表（REG_GUID、REG_GUID_V3_Encrypt、Unity PlayerPrefs）
  └─ PlatformProcess.exe / PlatformProcess.dll (1.13.0)  独立平台进程（token 转发）
  └─ webviewsdk.dll (1.41.0)       WebView 账号中心/网页登录
\`\`\`

游戏侧不直接读写 \`login_cache\`；它通过 U8SDK/HGSDK 的 native 接口取 token，
账号“缓存”的具体实现全部在 SDK 内。

---

## 2. 账号缓存位置与文件清单

根目录：\`%USERPROFILE%\\AppData\\LocalLow\\Hypergryph\\Endfield\`

| 路径 | 大小 | 作用 | 格式/保护 |
|---|---|---|---|
| \`sdk_data_<hash>\\login_cache\` | 4096 | 鹰角账号登录缓存（账号、token、remember 等） | MMKV，AES-CFB128，密文 |
| \`sdk_data_<hash>\\login_cache.crc\` | 4096 | login_cache 的 MMKV metaInfo（CRC、版本、sequence、随机 IV、actualSize） | 明文 |
| \`sdk_data_<hash>\\sdk_hgsdk_data_1\` | 4096 | 设备 GUID（\`sdk_hgsdk_data_1_guid\`），多次追加历史 | 明文 MMKV |
| \`sdk_data_<hash>\\mmkv.default\` | 4096 | HGSDK 默认 MMKV | 明文 |
| \`sdk_data_<hash>\\gameprotocol_cache\` | 4096 | 游戏协议缓存 | MMKV |
| \`sdk_data_<hash>\\gf_login_cache\` | 4096 | 旧/备用登录缓存（旧目录中存在） | MMKV |
| \`sdkdata\\sdk_hgsdk_config.bin\` | 384 | HGSDK 配置（加密） | 二进制 + 签名 |
| \`sdkdata\\sdk_hgsdk_config.gryph\` | 256 | 配置签名（RSA-2048 / Ed25519 级别） | 签名 |
| \`F:\\Endfield Game\\sdk_data.dat\` | 64 | SDK 数据密钥/种子文件 | 高熵二进制 |
| \`F:\\Endfield Game\\mmkv\\login_cache\` | 4096 | 旧版/备份的登录缓存（当前为空） | MMKV |
| \`U8Data\\config\\*.bin/.gryph\` | 1–14 KB | U8SDK 渠道配置 + 签名 | 加密 + 签名 |

注册表：

| 键 | 值 | 说明 |
|---|---|---|
| \`HKCU\\Software\\Hypergryph\\sdk_data\\f654bce49f1470d3852027a0da9f09a4\` | \`REG_GUID\` = \`5c0b4ec5-d205-4478-a62b-8006b56fd190\` | 缓存主 GUID（明文） |
| 同上 | \`REG_GUID_V3_Encrypt\`（263 B） | 同一 GUID 的 **DPAPI 密文**（V3 方案） |
| \`HKCU\\Software\\Hypergryph\\Endfield\` | \`u8sdk_cached_uid\` = \`974075392\` | 最近登录的游戏 UID |
| 同上 | \`PLDK_cachedUserId\` = \`974075392\` | PLDK 缓存用户 ID |
| 同上 | \`PLDK_cachedRoleId\` = \`1788316250\` | PLDK 缓存角色 ID |
| 同上 | 其余 \`audio_*\`、\`video_*\`、\`Screenmanager*\` | Unity PlayerPrefs |

\`REG_GUID_V3_Encrypt\` 的字节流以标准 DPAPI blob 头开始
（\`01 00 00 00 D0 8C 9D DF 01 15 D1 11 8C 7A 00 C0 4F C2 97 EB ...\`），
用 \`CryptUnprotectData\`（当前用户）可解出 36 字节 ASCII：
\`5c0b4ec5-d205-4478-a62b-8006b56fd190\`，与 \`REG_GUID\` 完全相同。
因此 \`REG_GUID_V3_Encrypt\` 只是 REG_GUID 的 DPAPI 保护副本，供 SDK 在需要主密钥时读取。

---

## 3. login_cache 的格式与加密

### 3.1 MMKV 文件结构

\`login_cache\` 是 MMKV 1.2.15 的 mmap 文件（固定 4096 字节页）：

\`\`\`
offset 0x00 : uint32 actualSize          // 当前有效数据长度，明文
offset 0x04 : data[actualSize]           // AES-CFB128 密文（实际有效载荷）
offset ...  : 0x00 填充
\`\`\`

\`.crc\` 文件是 MMKVMetaInfo：

\`\`\`
uint32 m_crcDigest;     // 数据 CRC32
uint32 m_version;       // = 3 (MMKVVersionActualSize)
uint32 m_sequence;      // 全量写回次数
uint8  m_vector[16];    // 随机 IV（fillRandomIV 由 rand() 生成，4 个 32 位值）
uint32 m_actualSize;
\`\`\`

本次观测（f654 目录）：

- \`login_cache\`：actualSize = 599（0x257），文件时间 2026-10-17 12:27:12
- \`login_cache.crc\`：CRC = 0xACAD917E，version = 3，sequence = 0x11（17 次全量写回），
  IV = \`7C 7C 00 00 11 13 00 00 43 10 00 00 0C 30 00 00\`，actualSize = 599

\`sequence = 17\` 表明该缓存历史上至少被完整重写 17 次（与版本更新/登录/账号切换对应）。

### 3.2 加密算法

反汇编与 MMKV 1.2.15 源码一致：

- \`AESCrypt\` 使用 **AES-CFB128**；key 取传入 \`cryptKey\` 的前 16 字节（AES-128），
  除非调用方使用 256 位变体（本机实测 AES-128 派生更符合）。
- 未显式提供 IV 时 IV = key；MMKV ≥ version 2 时改用 metaInfo 的随机 IV。
- 数据区从 offset 4 开始加密（\`Fixed32Size\` = 4）。

### 3.3 密钥来源（静态证据）

\`hgsdk.dll\` 中的关键字符串与反汇编：

- \`initLoginMMKV\`、\`sdk_data_path\`、\`sdk_data_\`、\`REG_GUID\`、\`REG_GUID_V3_Encrypt\`、
  \`strEncrykey\`、\`byteEncrykey is empty\`、\`getDeviceId\`、\`getDeviceId2\`
- 字符串 \`"REG_GUID_V3_Encrypt"\` 的引用位于 VA \`0x18007057c\` 等（\`QHgSdkData\` 的密钥处理），
  该函数先读注册表 \`REG_GUID_V3_Encrypt\`，失败时回退 \`REG_GUID\`；
- \`"strEncrykey"\` 的引用位于 VA \`0x180071205\` 附近：配置里 \`strEncrykey\` 为空时，
  SDK 会 \`create REG_GUID_V3_Encrypt\` 并把 **36 字节 GUID 字符串**写入内部 map；
- \`REG_GUID_V3_Encrypt\` 的实际内容是 DPAPI 保护的同一 GUID（已解出）。

**结论（静态推断）**：\`login_cache\` 的 MMKV cryptKey 来自 SDK 配置字段 \`strEncrykey\`；
首次运行/配置缺失时由 \`REG_GUID\`（DPAPI 保护）生成。MMKV 初始化时使用该值的
16/32 字节形式作为 AES 密钥。

> 限制：仅用 \`REG_GUID\` 的各种常见编码（ASCII/UTF-16/UUID 字节/大小端/MD5/SHA256/Base64）
> 以及 deviceId、machineguid、sdk_data.dat 的切片与哈希都未直接解出 599 字节明文，
> 说明 SDK 在传给 MMKV 之前还做了一层未还原的派生/编码（或使用配置中的另一随机密钥）。
> 该点需要动态读取进程内存或离线加载 hgsdk 调用其导出函数才能最终确认。

### 3.4 设备指纹

\`sdk_hgsdk_data_1\`（明文 MMKV）中键 \`sdk_hgsdk_data_1_guid\` 保存设备 GUID。
本机历史值（按写入顺序，最新在最后）：

\`\`\`
0d0db92f-03ab-4fe4-a0ce-75cbc55aa008
15fa7830-540d-43f3-8997-9ebedba3ba55
011d109c-db42-42a9-8f64-d270b5cf6288
3c685f85-2b99-4a37-8493-e93ddf30e966
e48abb46-de45-4a62-a3e9-65fdc64dc169
ecbf967b-4ac6-4450-af01-ed07c917e1bb
f8dfa737-775e-49f5-a4d0-734b8032926e
8e539a64-80a3-439a-9b9c-f434666c3bae
0ad3f472-31c0-401b-9c14-267549e2a45f
0c1b7f46-90fb-4626-a5ef-0ad254e88f8a
\`\`\`

运行日志（2026-01-21 首次运行）显示：
\`create guid:31a5fca7-c4f6-46b4-be7d-7f2e3c0915d0\`、\`machineguid:75efbf06-269d-4daa-819e-8be3c43ca1a7\`，
说明 deviceId 由 SDK 生成（\`create guid\`），MachineGuid 作为辅助指纹上报
（HTTP 头 \`X-DeviceId\`、\`X-DeviceId2\`，见 \`hgsdk.dll\` 字符串）。

---

## 4. 登录缓存的键与内容

从 \`hgsdk.dll\` 字符串与 xref 可确认 \`LoginImpl\` / \`UserInfoAssist\` 读写的键：

| 键 | 含义 | 证据 |
|---|---|---|
| \`hypergryph_id\` | 鹰角账号 ID（hgId） | \`"hypergryph_id"\` 字符串 + 多处 xref（0x1800468b8 等） |
| \`user_token\` | 账号 token | \`"user_token"\` xref 0x180056cf4、0x180057f06、0x18005a797 |
| \`user_account\` | 账号名/手机号掩码 | \`"user_account"\` xref 0x180056eea |
| \`remember_login\` | 记住登录（自动登录开关） | \`"remember_login"\` xref 0x18004972c |
| \`login_method\` | 登录方式（密码/验证码/扫码/第三方） | \`"login_method"\` 多处 xref |
| \`login_time\` | 最近登录时间 | \`"login_time"\` 字符串 |
| \`login_status\` | 登录状态 | \`"login_status"\` 字符串 |
| \`device_token\` / \`pass_token\` | 设备/通行 token | \`"device_token"\`、\`"pass_token"\` 字符串 |
| \`sessionToken\` / \`mmkvhgId\` | 会话 token / 账号 ID | \`"sessionToken is empty"\`、\`"mmkvhgId is empty"\` 日志字符串 |
| \`enableAutoLogin\` / \`rememberLogin\` | 自动登录/记住登录 UI 选项 | \`LoginWidget\` 字符串 |

对应的 UI 与行为字符串：

- \`QuickLoginWidget\`、\`AccountComboBox\`、\`AccountListPopup\`、\`refreshAccountList\`、
  \`getLastLoginAccount\`、\`quickLogin_text_lastLoginTime1..5\`、\`quickLoginDelete_*\`
  → 登录界面“最近登录账号”列表来自 \`login_cache\`；
- \`HgSdkAutoLogin\`、\`HgSdkGetAutoLoginAccount\`、\`HgSdkGetToken\`、\`HgSdkLogOut\`
  → SDK 导出接口，游戏通过 U8SDK 调用；
- \`LoginImpl::onUserBasicInfo\`、\`LoginImpl::onUserGranInfo\`、\`QHgSdkData::setLoginStatus\`、
  \`QHgSdkData::setUserBasicInfo\` → 登录成功后写缓存的位置。

网络接口（\`hgsdk.dll\` 字符串，均为 \`https://as.hypergryph.com\` 等）：

\`\`\`
/user/auth/v1/token_by_phone_password
/user/auth/v1/token_by_phone_code
/user/auth/v1/token_by_scan_code
/user/auth/v2/token_by_phone_code
/user/auth/v1/token_by_third_party_token
/user/oauth2/v2/grant
/user/oauth2/v1/grant_info
/user/info/v1/authenticate
/user/info/v1/token_by_cookie
/user/info/v1/logout
/user/info/v1/device_token_by_phone
\`\`\`

---

## 5. 运行时验证（2026-11-23）

通过 Starward（\`Start Endfield through parent bridge\`）启动客户端：

- 13:24:37 \`Endfield.exe\` 启动（Starward 播放时长记录开始）
- 13:25:38 HGSDK 1.28.0.10 初始化（\`SdkInitStatus:1\`）
- 13:25:40 读取 \`guid:31a5fca7-...\` 与 \`machineguid:75efbf06-...\`
- 13:25:41.333 \`LoginStatus:1\`（开始登录）
- 13:25:41.334 NetworkEventId:401（OAuth grant 请求）
- 13:25:41.490 \`HG hgId:1356065447097\`
- 13:25:41.490 LoginStep 2 → 3 → 4 → 5 → 6
- 13:25:41.658 \`[UID] HG channel uid:1559972306756\`
- 13:25:41.659 LoginStep 7 → 8，\`LoginStatus:2\`（登录完成）

**整个过程没有出现账号密码/扫码/验证码界面**，登录用时约 0.33 秒，证明使用了 \`login_cache\`
中持久化的凭据。登录过程中（13:41）文件 mtime 未立即变化；游戏退出后复查，
\`sdk_data_f654bce49f1470d3852027a0da9f09a4\login_cache\` 的 actualSize 已从 599 变为 2221，
说明本次运行期间 SDK 仍然写入了缓存（登录状态/账号列表/token 刷新），
只是 mmap 写入的磁盘可见时间滞后于登录时刻。缓存写入的触发点包括首次登录、
token 刷新、切换账号或登出。

游戏标题界面右上角存在“账户登出”入口；左下角为退出按钮。
PlayerPrefs 中的 \`u8sdk_cached_uid\`/\`PLDK_cachedUserId\`/\`PLDK_cachedRoleId\`
在本次登录后保持为 \`974075392\`/\`1788316250\`。

> 说明：本次运行时尝试读取 \`Endfield.exe\` 进程内存以提取 MMKV 明文键值，
> 但 \`OpenProcess(PROCESS_VM_READ)\` 返回拒绝访问（错误 5），进程受游戏保护，
> 非管理员会话无法读取；因此未获得 token 明文。该限制不影响上述文件/日志级结论。

---

## 6. 游戏层（角色）缓存

IL2CPP 元数据（\`global-metadata.dat\`，版本 29，172 个程序集）中与账号/缓存相关的类型：

| 类型 | 关键方法 | 作用 |
|---|---|---|
| \`Beyond.LoginManager\` | \`_LoginWorkFlowCoroutine\`、\`_PrepareBeforeLoginProcess\`、\`_ClearWorkflowAndContext\`、\`get_quickStart\`、\`get_firstLogin\`、\`MockManualSelectServer\` | 登录流程编排、区服选择、快速启动 |
| \`Beyond.LoginController\` | \`InitUI\`、\`MockLoginWithCallback\`、\`GetEnterGamePanelPredelay\` | 登录 UI |
| \`Beyond.PlayerLocalData\` | \`get_cachedUserId\`、\`get_cachedRoleId\`、\`_AppendUserToKey\`、\`GetUserString\`、\`SetUserString\`、\`GetCommonInt\`、\`SetCommonInt\` | 按用户 ID 追加键名，读写角色级键值 |
| \`Beyond.Gameplay.Core.ClientDataManager\` | \`SaveLocalData\`、\`_DoSaveLocalData\`、\`SaveUserData\`、\`_DoSaveUserData\` | ClientData JSON 落盘 |
| \`Beyond.Gameplay.Core.PlayerPrefsValueSource\` | \`Save\`、\`Get\`、\`Set\`、\`Remove\` | PlayerPrefs 封装 |
| \`HGU8SDKSetUserDataPath\` | — | 把“用户数据目录”传给 U8SDK/HGSDK |

ClientData 目录结构（已观测）：

\`\`\`
ClientData\Local\Default.json                    本机级 UI 状态（6880 B）
ClientData\User\974075392\Default.json           角色级 UI 状态（118 KB）
ClientData\User\974075392\Dungeon.json
ClientData\User\974075392\EquipPresetGroup.json
ClientData\User\974075392\FacBuildListRedDot.json
ClientData\User\974075392\InnRedDot.json
ClientData\User\974075392\LoadingTips.json
ClientData\User\974075392\SNS.json
ClientData\User\974075392\spaceship.json
ClientData\User\974075392\WEP.json
\`\`\`

JSON 采用 \`{ "intData": { key: {value, timestamp} }, "longData": {}, "floatData": {}, "boolData": {} }\`
结构，对应 \`PlayerLocalData\` 的按类型分桶存储。

---

## 7. 写入时机与生命周期（综合结论）

\`\`\`
启动
 └─ HGSDK init → initLoginMMKV(path=sdk_data_<hash>, cryptKey=strEncrykey)
      ├─ 无 login_cache → 游客/首次登录：显示登录 UI，成功后写 login_cache
      └─ 有 login_cache → HgSdkGetAutoLoginAccount / HgSdkAutoLogin
            ├─ 读取 remember_login、hypergryph_id、user_token
            ├─ POST /user/oauth2/v2/grant（或 /user/info/v1/authenticate）
            ├─ LoginStep 1..8 → LoginStatus 2
            └─ 不重写 login_cache（除非 token 刷新）
登录成功/切换账号
 └─ LoginImpl::onUserBasicInfo / QHgSdkData::setUserBasicInfo
      └─ 写 hypergryph_id、user_account、user_token、login_method、login_time
登出
 └─ HgSdkLogout / /user/info/v1/logout
      └─ 清除 login_cache 中 token（保留或删除账号记录，取决于 logout_keep/logout_delete）
游戏内角色数据
 └─ 登录后 ClientDataManager.SaveUserData → ClientData\User\<uid>\*.json
      └─ PlayerLocalData 写入 PlayerPrefs 的 u8sdk_cached_uid/PLDK_cached*
\`\`\`

---

## 8. 证据与限制

**直接观测**

- \`login_cache\` 为 MMKV 1.2.15 文件：actualSize=599、.crc metaInfo version=3、随机 IV、sequence=17
- \`REG_GUID_V3_Encrypt\` 是 DPAPI blob，可解出 \`REG_GUID\` 同值
- \`sdk_hgsdk_data_1\` 明文保存 10 条设备 GUID 历史
- HGSDK 日志：自动登录完整时间线（LoginStatus 1→2，hgId、HG channel uid）
- 自动登录前后 \`login_cache\` mtime 不变
- PlayerPrefs 中的 UID/角色 ID 缓存
- IL2CPP 类型/方法名与字符串字面量（\`LoginManager\`、\`PlayerLocalData\`、\`ClientDataManager\`）

**静态推断（需动态确认）**

- \`login_cache\` 的 AES key 具体字节：由 \`strEncrykey\`（配置）派生；用 REG_GUID 的常见编码未解出明文，
  实际派生步骤未还原
- 写入 \`login_cache\` 的确切函数与触发条件（登录成功、token 刷新、登出、账号切换），
  基于 \`LoginImpl::onUserBasicInfo\`、\`QHgSdkData::setUserBasicInfo\` 等符号与 MMKV 行为推断

**限制**

- 普通权限下 \`OpenProcess(PROCESS_VM_READ)\` 返回错误 5；随后以**管理员权限**重试（\`RunAs\` 提权，\`admin=True\`、\`SeDebugPrivilege=True\`）：
  \`OpenProcess(0x0410)\` 成功，\`VirtualQueryEx\` 枚举到 9994 个内存区域，
  但 \`ReadProcessMemory\` 对全部 9505 次可读区域读取均失败（\`readOK=0\`）。
  原因是游戏运行期间 AntiCheatExpert 内核驱动在位：
  \`ACE-ADVT\`、\`ACE-CORE106010071\`、\`ACE-CORE206010071\` 处于 Running 状态，
  对 \`Endfield.exe\` 实施进程读保护。
- 该保护属于反作弊的预期设计，未尝试绕过（注入、调试器附加、驱动对抗等）。
- 未修改任何游戏文件；运行时通过 Starward 正常启动并登录（两次：13:24 与 13:40）。

---

## 9. 进一步验证建议

1. 以管理员权限 + 内核 ETW/ProcMon 观察 \`Endfield.exe\` 对 \`login_cache\`、\`sdk_hgsdk_data_1\`、
   \`sdk_data.dat\` 的读写时序（只读观测，不注入）。
2. 离线加载 \`hgsdk.dll\` 并调用导出函数 \`HgSdkInit\`/\`HgSdkGetToken\`，
   在 \`mmkvWithID\` 处下断点读取 \`cryptKey\`（需要还原 SDK 初始化参数，风险低于在线注入）。
3. 在 \`login_cache\` 存在时删除/重命名该文件，再启动客户端，观察登录是否退化为需要输入凭据，
   以验证“自动登录完全依赖该缓存”。
4. 在游戏内执行“账户登出”，观察 \`login_cache\`/\`.crc\` 的 mtime 与 actualSize 变化，
   确认登出时的清除策略（\`logout_keep\` vs \`logout_delete\`）。

---

## 附录 A：关键文件哈希（本次分析对象）

| 文件 | SHA-256 |
|---|---|
| \`Endfield.exe\` | （如需可补） |
| \`GameAssembly.dll\` | （如需可补） |
| \`hgsdk.dll\` | \`5127c127d610db9709a44e7e3ce63ae3d089dad02cc4819fabaa437241c2aab3\` |
| \`U8SDK.dll\` | \`4cd6ff5cdeb6d56eb0ab44d0663cf3eaa1f184a988ff5129163f5f326d9a74d9\` |

## 附录 B：关键字符串（hgsdk.dll）

\`\`\`
login_cache
sdk_data_path / sdk_data_
REG_GUID / REG_GUID_V3_Encrypt
strEncrykey / byteEncrykey
initLoginMMKV
getDeviceId / getDeviceId2
sdk_hgsdk_data_1_guid
hypergryph_id / user_token / user_account / remember_login / login_method / login_time
sessionToken is empty / mmkvhgId is empty
HgSdkAutoLogin / HgSdkGetAutoLoginAccount / HgSdkGetToken / HgSdkLogOut
UserInfoAssist::getMMKVUserInfo
QHgSdkImpl::initLoginMMKV
\`\`\`

## 附录 C：本次运行的账号标识（本地观测）

| 项 | 值 |
|---|---|
| 鹰角 hgId | 1356065447097 |
| HG channel uid | 1559972306756 |
| 游戏 UID | 974075392 |
| 角色 ID | 1788316250 |
| 设备 GUID（最新） | 0c1b7f46-90fb-4626-a5ef-0ad254e88f8a |
| MachineGuid | 75efbf06-269d-4daa-819e-8be3c43ca1a7 |
| REG_GUID | 5c0b4ec5-d205-4478-a62b-8006b56fd190 |
