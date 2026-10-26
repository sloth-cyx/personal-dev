# 静阅 Android 1.0.0 — 源码与构建

交付日期：2026-10-26，Asia/Kuala_Lumpur。

## 架构

- 原生 Java Activity 负责 Android 系统文件选择、内容 URI、后台导入、私有存储、字体导入、系统栏及退出行为。
- 本地 WebView 展示专为手机设计的阅读界面。页面和资源全部来自 APK 或应用私有书库；不申请联网权限。
- 小说导入后生成 `blocks.bin` 和 `index.bin`，按索引读取相邻正文，阅读界面只保留有限数量的段落。图片按需展示，较大位图导入时下采样。
- `library.json` 保存书库索引、设置、书签和阅读位置；写入时保留上一份有效备份。重复书籍按 SHA-256 去重。
- 内置完整霞鹜文楷字体及 OFL 许可，应用本身不依赖第三方 Android 库。

## Windows 下构建

建议把源码复制到开发工作目录，避免在交付目录生成临时构建文件。需要 Windows x64 和 PowerShell 7。

```powershell
.\setup-tools.ps1
.\build.ps1 -Signing '你的签名备份目录'
```

`setup-tools.ps1` 从 Google / Azul 官方地址下载固定版本，验证 SHA-256 后解压 Java 17、Android 35 平台与 Build Tools 35.0.0。不会安装系统服务、修改系统 PATH 或安装虚拟化驱动。下载工具自带其各自许可文件。

编译产物在 `build\JingYue-1.0.0.apk`。`-Tools` 可以指定已经准备好的工具目录。

正式更新必须使用本次交付的 `签名备份`，同时增加 AndroidManifest.xml 中的 versionCode。不要重新生成密钥后覆盖发布；不同签名无法直接升级原应用。如果没有提供签名目录，构建脚本会在 `signing` 创建新密钥，仅适合全新安装。

## 检查

```powershell
.\build.ps1 -Test
.\build.ps1 -Test -Samples '你的小说测试目录'
```

解析测试用纯 JVM 执行，覆盖中文编码、章节、EPUB 导航、DOCX 样式继承、FB2、HTML、长行代理对、18 万段索引与随机读取。临时样本由测试生成。

`tests\browser-test.cjs` 使用 Playwright + Chrome 验证手机布局、连续滚动、翻页、图片尾部、书签、触控、横屏与删除流程。运行时通过 `PLAYWRIGHT_PATH` 指向已安装的 Playwright；脚本默认使用当前构建环境的路径，可以自行更改 Chrome 路径。

`tests\build-device-tests.ps1` 可生成 Android instrumentation 测试包。它只供专用测试设备使用，不能分发给普通用户；测试包包含测试文件 Provider。正式 APK 没有这个 Provider。

本次已完成 JVM、浏览器手机布局、正式 APK 编译及签名检查。本机没有可用的模拟器加速驱动，多个官方软件模拟配置未能启动，因此 instrumentation 尚未实际运行，尚未完成 Android 真机安装验证。

## 主要文件

- `src/com/jingyue/reader/MainActivity.java`：系统集成、导入、存储、受限本地资源服务。
- `src/com/jingyue/reader/BookParser.java`：流式文本索引、EPUB/DOCX/FB2/HTML 解析。
- `assets/app.js`、`assets/style.css`、`assets/index.html`：手机界面、有限窗口正文、分页与位置保存。
- `assets/LXGWWenKai-Regular.ttf`、`assets/OFL-WenKai.txt`：原始开源字体与完整许可。

字体来源：https://github.com/lxgw/LxgwWenKai 。字体未修改，按 SIL Open Font License 1.1 随应用分发。
