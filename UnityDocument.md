**翻译自官方文档https://docs.smartlydressedgames.com/en/stable/u3-sdk/unity-project.html**

**Unturned Unity 项目概述**

**下载**
Unturned 的项目文件使用 Git 版本控制系统（VCS）存储。

如果你安装了 Git 命令行工具，可以使用以下命令将文件克隆到本地电脑：

```
git clone https://github.com/SmartlyDressedGames/U3-SDK.git
```

**入门指南**
你需要使用与“安装 Unity”部分所述相同版本的 Unity 编辑器。可以在 `.ProjectSettings/ProjectVersion.txt` 中再次确认编辑器版本。

Steam 必须正在运行，且 Unturned 必须已安装。（创意工坊模组和大型二进制文件会从游戏的最新官方发行版中加载。）

要在编辑器中运行游戏，请打开 `GameStartup.unity` 场景并点击播放。

> **提示**
> 我们建议在游戏运行时关闭 Unity 的层级窗口，除非你需要用到它。Unturned 的场景出于优化目的，主要由顶层游戏对象构成，但这会导致层级窗口变慢。更多信息可参阅场景结构 > 层级深度与数量。

**编辑器偏好设置**
遗憾的是，Unturned 不支持热重载。如果在 Unity 打开期间修改代码，我们建议将“游戏过程中脚本更改”选项设为“停止游戏后重新编译”。

**运行模式设置**
通过菜单 `Window > Unturned > Editor Settings` 可打开一个编辑器窗口。它主要用于设置那些原本在命令行中指定的选项。

*   **Auto Load Level 与 Auto Load Mode**：设置为某个关卡文件夹名称，即可跳过菜单，从加载画面直接进入单人游戏或关卡编辑器。
*   **Glazier**：覆盖默认的 Glazier 设置。

**故障排除**
请查看 Unity 的日志文件。在 Windows 上，项目文件夹中有一个指向最新日志文件 `.Unity Editor.log` 的快捷方式，以及指向存放日志的文件夹 `UnityEditor Logs Folder` 的快捷方式。

**文件组织结构**
*   `Assets/Game/Sources` 包含所有用于导出为资源包（Asset Bundle）的 Unity 资源的源文件（如 `.blend`）和导入文件（如 `.fbx`）。
*   `Assets/Resources` 存放由 `Resources` 类加载的 Unity 资源。应尽可能避免向此文件夹添加新文件。
*   `Assets/Runtime` 包含所有玩家代码。某些较新的功能有其按程序集定义划分的独立文件夹，但绝大多数游戏代码都在 `Assembly-CSharp` 文件夹中。如果能重命名会更好，但据我所知（截至 2024-10-18），这样做会破坏资源包中的脚本引用，因此不能重命名。
*   `Assets/Runtime/Assembly-CSharp/NetGen` 是所有自动生成的网络代码。它被包含在 Git 中，以使首次运行流程更顺畅。
*   `Builds` 文件夹包含导出的 Unity 播放器（游戏构建版本）。

**网络代码**
大多数游戏玩法都需要远程过程调用（RPC）才能正常运行。即使是单人游戏，本质上也是一个只有一名玩家的服务器。RPC 代码是自动生成的，但在编辑器中这仍是一个手动步骤：

1.  打开 `Window > Unturned > Net Gen`
2.  点击 `Generate`
3.  切换窗口再切回来，以确保脚本被导入

**持续集成**
对于每次提交，我们都设置了 Jenkins 来构建项目并运行测试，并可选择上传到 Steam 分支。

在撰写本文时（2024-10-18），Jenkins 服务器是在本地托管的，无法通过互联网访问。

它主要使用位于 `Build_Scripts/Jenkinsfile.txt` 的脚本通过流水线（Pipeline）来工作。

启动正确版本的 Unity 依赖于从项目根目录 `Build_Scripts/JenkinsBootstrapper` 构建的 `JenkinsBootstrapper.exe`。它期望 Unity 安装在以下路径之一：

*   `C:\UnityEditors`
*   `C:\Unity Editors`
*   `C:\Program Files\Unity\Hub\Editor`

（是的，很遗憾，开发和构建流程都非常依赖 Windows 环境。）

---

**主要内容介绍**

这份文档是 Unturned 游戏官方提供的 Unity 项目开发指南，核心目的是帮助开发者搭建本地环境、理解项目结构并顺利开展开发工作。主要内容可归纳为以下几点：

1.  **环境搭建依赖强**：项目不仅需要特定版本的 Unity 编辑器，还高度依赖 Steam 平台。**Steam 必须运行且安装了 Unturned 游戏本体**，因为项目会直接从游戏发行版中加载创意工坊模组和大型二进制文件，无法独立运行。
2.  **不支持热重载**：这是非常关键的一点。修改 C# 代码后，游戏不会自动更新，必须停止运行并等待脚本重新编译完成，否则会导致问题。
3.  **专用的编辑器工具**：项目提供了自定义的编辑器窗口（`Window > Unturned`），用于设置测试启动参数（如直接加载某个关卡，跳过主菜单）以及生成至关重要的网络通信代码（RPC）。网络代码的生成虽已自动化，但仍需手动触发。
4.  **独特的文件结构**：游戏逻辑代码绝大部分集中在 `Assembly-CSharp` 中，且因资源包引用问题无法重构更名。`NetGen` 文件夹存放生成的网络代码，已入库以减少首次配置的繁琐度。官方特别强调要避免向 `Assets/Resources` 文件夹添加新文件。
5.  **单机即服务器**：文档点明了一个核心架构——即便是单人游戏，本质上也是运行了一个仅有一名玩家的服务器，所有玩法都依赖于 RPC 调用。
6.  **本地化的 Windows 构建流程**：项目的持续集成基于本地 Jenkins 服务器，构建脚本和启动器均围绕 Windows 系统设计，并硬编码了 Unity 编辑器的常见安装路径，表明其开发和构建环境完全以 Windows 为中心。