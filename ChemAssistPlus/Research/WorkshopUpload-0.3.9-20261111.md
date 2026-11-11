# 0.3.9 工坊发布核验

- 上传完成：2026-11-11 22:53:54（Asia/Hong_Kong）。
- 条目：3836818025；AppID：4348910；公开visibility=0；内容ManifestID：5748090467896481135。
- DLL SHA256：CFAC97AEB9A2E5700902F9DFEC514BD27388415C8D961FBA63ED327F7441C4D5。
- 复用既有SteamCMD缓存登录，-tcp上传同条目；console_log.txt本次时间有Committing update / Success，workshop_log.txt同时间有Upload finished for workshop item 3836818025 : OK。
- Steam公开GetPublishedFileDetails API核验：result=1，hcontent_file=5748090467896481135，visibility=0；默认description与上传工作区文本逐字一致，7954 UTF-8字节。
- CHANGELOG与WorkshopChangelog共18个有记录版本，顺序一致；模板同步当前功能，鸣谢保留。独立schinese简介本次未修改或核实。
- 上传工作区回执：WorkshopUpload/upload-result-0.3.9.json；同步前备份：WorkshopUpload/archive/before-prepare-0.3.9-20261111-225322-135。
- 本次不部署、不打包、不启动游戏、不改存档；原生扫描PASS（409 methods，capabilities none），未新增或运行测试。订阅下载和实际加载需分开核验。
