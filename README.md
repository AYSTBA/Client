<p align="center">
  <img src="https://github.com/Rhythia/Client/blob/master/textures/RhythiaSquircle.png?raw=true" alt="Rhythia" width="400"/>
</p>

<h3 align="center">A free-to-play rhythm game where you move your mouse across a 3×3 grid.</h3>

<p align="center">
  <a href="https://discord.gg/rhythia"><img src="https://img.shields.io/discord/1064060807320702996?label=Discord&logo=discord&logoColor=white&color=5865F2" alt="Discord"></a>
  <a href="https://godotengine.org/download"><img src="https://img.shields.io/badge/Godot-4.7-%23478CBF?logo=godot-engine&logoColor=white" alt="Godot 4.7">
</p>

---

> *"Rhyt-Rhythmia"*
> — Edward "BTMC" Ling, 2024

> [!CAUTION]
> **Rhythia is NOT affiliated with CAPO Games, Steam Rhythia, or rhythia.com in any way.** We cannot provide support or assistance for any issues related to those platforms. Please ensure you are downloading the correct client from the links below. We do, however, support any modification of our logos and content in the client source under our [license](LICENSE).

---

## Table of Contents

- [About](#about)
- [Chinese Localization](#chinese-localization)
- [Downloading the Game](#downloading-the-game)
  - [Rewrite (Current)](#rewrite-current-maintained-client)
  - [Nightly (Legacy)](#nightly-legacy)
- [Installation](#installation)
  - [Windows](#windows)
  - [Linux](#linux)
  - [Linux Troubleshooting](#linux-troubleshooting)
- [User Folder](#user-folder)
- [Getting Maps](#getting-maps)
- [Development](#development)
  - [Prerequisites](#prerequisites)
  - [Project Structure](#project-structure)
- [Contributing](#contributing)

---

## About

**Rhythia** (formerly *Sound Space Plus*) is a free and open-source rhythm game built with **Godot 4.7** and **C#**.

---

## Chinese Localization

> 本仓库是 Rhythia 的**简体中文汉化版本**，汉化：**AYSTBA**。
> This repository is a Simplified Chinese build of Rhythia. Translation by **AYSTBA**.

### 切换语言 / Switching language

设置 → **语言**（分类栏第一项）→ 选择 `English` 或 `简体中文`。

切换后需要**重启游戏**才会生效，因为部分界面在改语言时已经构建完成。游戏会弹出重启提示。

原版游戏本身不带任何多语言支持，界面只有英文一种，因此语言列表为 `English`（原版界面）与 `简体中文` 两项。

> [!NOTE]
> The original client ships no localization at all — every UI string was hardcoded English — so `English` and `简体中文` are the only two entries in the language list.

### 汉化署名 / Credit

中文模式下，**主菜单**与**设置界面**的右下角会显示 `AYSTBA 汉化`。切换到 English 时该署名自动隐藏，因为它署的是这份中文翻译。

### 词条维护 / Editing the translation

所有译文集中在 [`translations/strings.csv`](translations/strings.csv)，用表格软件或文本编辑器直接编辑即可。设计上有两个要点：

- **英文原文就是 key**。Godot 4 的 Control 节点默认开启 auto-translate，会拿节点上的原文去查表，因此场景里几百处文案无需改动即可翻译。
- 代码里动态生成的文案（提示条、弹窗、设置项）走 `L10n.Tr(...)`，其中 toast 与弹窗是在各自唯一出口处统一翻译的。

改完 CSV 后需要让 Godot 重新导入一次，它才会重新生成 `.translation` 资源：

```bash
godot --headless --path . --import
```

### 中文字体 / CJK font

界面字体（Comfortaa / Montserrat / Nunito / Roboto / Ubuntu）都只含拉丁字形。中文模式启动时会把系统中文字体（微软雅黑 / Noto Sans CJK / 思源黑体等）注册为回退字体，缺字时自动使用，不必额外打包字体文件。

### 手柄支持 / Gamepad support

原版 `PlayerInputController` 只处理鼠标与键盘事件，手柄完全没有输入通路。本版本补上了：

| 输入 | 作用 |
|---|---|
| 左/右摇杆（取推动较大的一侧） | 移动光标 |
| 十字键 | 移动光标（精确微调） |
| **A** | 空格（暂停 / 跳过） |
| **Start** | 暂停 |
| **X** | 切换音符回推 |
| **Y** | 切换音符淡出 |
| **Back** | 切换正交视角 |

摇杆位移会被换算成与鼠标相同的相对像素增量，所以所有摄像机模式无需修改即可工作。手感可在 **设置 → 输入与输出** 里调整：

- **手柄光标速度**（默认 700 像素/秒，100–3000）
- **手柄摇杆死区**（默认 0.15，0–0.5）

> [!NOTE]
> 手柄目前只在**对局内**控制光标，菜单导航仍需鼠标（原版按钮为不可聚焦状态）。

### 修复 / Fixes

- **中文谱面名的回放无法观看**：回放文件里字符串的长度前缀写的是字符数，而读取端按 UTF-8 字节数消费，导致 `晨光 Morning Light` 被截断成 `晨光 Morning L`、找不到谱面目录。已改为写入字节数（ASCII 名字行为不变）。
- **没有谱面时点「随机」报错**：`Random.Next(0)` 返回 0 而不抛异常，随后 `Maps[0]` 越界。已加空列表判断。
- **空谱面列表没有任何提示**：首次启动且未导入谱面时，列表空白容易误以为是菜单坏了。现在会提示去「导入」。

### 导出注意事项 / Exporting

- 必须使用 **Godot 4.7.2 (.NET)** 与 **.NET SDK 10**；非 .NET 版编辑器会改写 `project.godot` 的构建标记并破坏 `.godot` 缓存。
- 导出的 `.pck` **不包含** `translations/strings.csv`（带 `.import` 的文件只导出导入产物），运行时会改为读取 `.translation` 资源。因此改完译文后、导出之前务必先执行一次上面的 `--import`，否则导出包里会是没有新改动的旧译文。
- 命令行导出示例：

  ```bash
  godot --headless --path . --export-release "Windows Desktop" "../Rhythia-Windows/Rhythia.exe"
  ```

---

## Downloading the Game

There are currently two builds of the game available.

> [!IMPORTANT]
> **Do not** clone this repository to play the game. Use the download links below instead.

### Rewrite (Current Maintained Client)

The current client being actively maintained, now in early testing.

> [!NOTE]
> **Recommended** — This is the latest version of the client. Please report any bugs found in **#bug-reports** on our [Discord](https://discord.gg/rhythia) server.

You can view all releases on the [Rewrite releases page](https://github.com/Rhythia/Client/releases).

| Platform | Direct Download |
|---|---|
| **Windows** | [Download](https://github.com/Rhythia/Client/releases/latest/download/windows.zip) |
| **Linux** | [Download](https://github.com/Rhythia/Client/releases/latest/download/linux.zip) |

You can view the Rewrite source code [here](https://github.com/Rhythia/Client).

### Nightly (Legacy)

The legacy client we all know and (somewhat) love. We recommend this client if you're just starting out for now.

> [!WARNING]
> **Nightly is no longer being maintained.** All current development efforts are focused on the Rewrite client above.

You can view all releases on the [Nightly releases page](https://github.com/David20122/sound-space-plus/releases).

| Platform | Direct Download |
|---|---|
| **Windows** | [Download](https://github.com/David20122/sound-space-plus/releases/latest/download/windows.zip) |
| **Linux** | [Download](https://github.com/David20122/sound-space-plus/releases/latest/download/linux.zip) |

You can view Nightly's source code [here](https://github.com/David20122/sound-space-plus).

---

## Installation

### Windows

1. Download `windows.zip` for your chosen client above.
2. Extract the `.zip` file to a folder of your choice.
3. Run `Rhythia.exe`.

> [!WARNING]
> You **must** have the [Visual C++ Redistributable](https://aka.ms/vs/17/release/vc_redist.x64.exe) installed to run the game on Windows!

### Linux

#### GUI File Manager

> [!TIP]
> The following packages are recommended: `thunar`, `thunar-archive-plugin`

1. Download `linux.zip` for your chosen client above.
2. Right-click the downloaded file and extract it.
3. Run `Rhythia.x86_64`.

#### Terminal

> [!WARNING]
> The `unzip` package is **required**. Install it using your distribution's package manager if you don't have it.

1. Download `linux.zip` for your chosen client above.
2. Open your terminal and run the following commands:
   ```bash
   # Assuming the file is in the Downloads folder
   cd $HOME/Downloads
   unzip -d Rhythia linux.zip
   ```
3. Move into the extracted folder and run the game:
   ```bash
   cd Rhythia
   ./Rhythia.x86_64
   ```

### Linux Troubleshooting

If your game doesn't run, make the binary executable from within your Rhythia folder:

```bash
sudo chmod +x Rhythia.x86_64
```

> [!NOTE]
> You need to be a superuser in order to run `sudo`.

Still having issues? Visit the **#support** channel in our [Discord](https://discord.gg/rhythia).

---

## User Folder

Your maps, replays, skins, and settings are stored separately from the game installation:

| Platform | Path |
|---|---|
| **Windows** | `%appdata%\Rhythia` |
| **Linux** | `~/.local/share/Rhythia` |

You can also access the user folder from within the game via the **User Folder** button on the top bar.

> [!NOTE]
> You can change the user folder location to your desired path in **Settings** → **Other** → **Set User Folder Path**

### 便携模式 / Portable builds

在 `Rhythia.exe` 同级目录放一个名为 `portable.txt` 的文件（内容随意），游戏就会把全部数据写进同级的 `user/` 目录，不再使用系统用户目录：

```
D:\Rhythia-Player\
├── Rhythia.exe
├── portable.txt
├── data_Rhythia_windows_x86_64\
└── user\                 ← 谱面、存档、配置、数据库都在这里
```

这样做的用途是**把个人游玩版和开发版彻底分开**：从源码运行的开发版仍使用 `%appdata%\Rhythia`，便携版用自己目录下的 `user\`，两边的谱面、配置、成绩与数据库互不影响。想换机器或备份时，直接拷贝整个文件夹即可带走全部数据。

> [!NOTE]
> 便携模式优先级最高，会忽略「设置 → 其他 → 用户文件夹路径」。另外 Godot 引擎自身的日志（`godot.log`）由项目设置决定，仍会写到 `%appdata%\Rhythia\logs`，不影响游戏数据。

---

## Getting Maps

Maps aren't included in the game by default; they must be downloaded and imported into the client. Maps can be found in the Discord server linked above.

---

## Development

### Prerequisites

| Tool | Version | Notes |
|---|---|---|
| [Godot Engine](https://godotengine.org/download) | **4.7** | .NET (C#) build required |
| [.NET SDK](https://dotnet.microsoft.com/download) | **10.0** | |
| [Git LFS](https://git-lfs.github.com/) | Latest | Required for large binary assets |

### Project Structure

```
├── fonts/           # Font assets
├── meshes/          # 3D mesh assets
├── prefabs/         # Reusable scene prefabs (UI elements, etc.)
├── scenes/          # Main game scenes (menu, game, results, etc.)
├── scripts/         # C# source code
│   ├── database/    # Database / persistence layer
│   ├── game/        # Core gameplay (attempts, renderers, mods, judgments)
│   ├── map/         # Map parsing and management
│   ├── multiplayer/ # Multiplayer lobby and player logic
│   ├── scenes/      # Scene-specific scripts
│   ├── shaders/     # Shader code
│   ├── skinning/    # Skin loading and management
│   ├── spaces/      # Space-related logic
│   ├── ui/          # UI components and notifications
│   └── util/        # Utility / helper classes
├── sounds/          # Audio assets
├── textures/        # Texture and image assets
├── themes/          # Godot UI themes
└── user/            # Default user data scaffold
```

---

## Contributing

We ❤️ developers! Contributions are welcome — whether it's bug fixes, features, documentation, or translations.

> [!WARNING]
> **Nightly is no longer actively maintained.** This guide is for contributing to the **Rewrite** client. The contribution process is the same, but you'll be working with the [Rewrite repository](https://github.com/Rhythia/Client).

### Required Software

| Software | Notes |
|---|---|
| [Godot 4.7 Stable (.NET)](https://godotengine.org/download) | The C# / .NET build is required |
| [Git](https://git-scm.com/downloads) | Windows: Git for Windows · Linux: `git` package |
| [GitHub Account](https://github.com/signup) | Needed to fork and open pull requests |

**Optional:** [Visual Studio Code](https://code.visualstudio.com/) with the [godot-tools](https://marketplace.visualstudio.com/items?itemName=geequlim.godot-tools) extension.

### Setting Up Your Workspace

#### 1. Fork the Repository

1. Head to the [Rewrite repository](https://github.com/Rhythia/Client).
2. Press the **Fork** button and create a fork under your account.

#### 2. Clone Your Fork

Open a terminal and clone your fork into a folder of your choice:

```bash
# Navigate to your preferred directory
cd Documents/Rhythia

# Clone your fork (replace YOUR_USERNAME with your GitHub username)
git clone https://github.com/YOUR_USERNAME/Client.git

cd Client

# Fetch large files (textures, audio, etc.)
git lfs fetch --all
git lfs pull

# Restore .NET tools
dotnet tool restore
```

#### 3. Open in Godot

1. Open **Godot 4.7 (.NET)**.
2. Click **Import**.
3. Browse to your cloned repository folder and select `project.godot`.
4. Click **Open**.

### Submitting Your Changes

1. **Format** the project:
   ```bash
   dotnet format Rhythia.csproj
   dotnet csharpier format .
   ```

1. **Stage** any new files you created:
   ```bash
   git add ./path/to/your/new/files
   ```

2. **Commit** and **push** your changes:
   ```bash
   git commit -m "Brief description of your change"
   git push origin indev
   ```

3. Go to your forked repository on GitHub — you should see a message like *"This branch is 1 commit ahead of Rhythia/Client:indev"*.

4. Click **Contribute → Open pull request**.

5. Ensure your PR is targeting the **`indev`** branch of the main repository.

6. Review your changes, add a clear title and description, then click **Create pull request**.

7. Wait for maintainers to review your code and provide feedback.

### Guidelines

- Follow existing code style and naming conventions.
- Keep PRs focused — one feature or fix per PR.
- Test your changes locally before submitting.
- For large changes, open an issue first to discuss the approach.
- Always state AI usage in PRs and issues, you should be able to explain and fix any code you submit.

---

## License

Rhythia is licensed under the [GNU Affero General Public License v3.0](LICENSE).
