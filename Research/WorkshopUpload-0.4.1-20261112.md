# 0.4.1 工坊发布核验

- 用户授权顺序：先Git提交，后发布0.4.1。追加“推进新版本前先提交旧版本遗留内容”约定；用户明确本次不用拆分提交，后续执行。约定已写AGENTS.md和DEVELOPMENT.md。
- 发布前提交：bc262f7 — Release 0.4.1 with dry-product merging and chemical progress bars。含此前未提交的0.3.9/0.4.0内容及本次0.4.1；未推送远端。
- 最终构建：0.4.1，0警告0错误；原生ModScanner PASS，417 methods，capabilities none。遵循开发约定未新增/运行测试。DLL SHA256：A9934C18F114BC6A82E5229EE8B0B633F9E2F03696ADB67F3018A1C0303B2097。
- prepare-workshop.ps1核对构建凭据/三文件哈希、20个历史版本集合一致，保留工坊ID、封面、公开状态和鸣谢。备份至游戏根WorkshopUpload/archive/before-prepare-0.4.1-20261112-005552-858；最终简介7982 UTF-8字节、139行，BBCode与本地文本一致，小于8000字节。
- publish-workshop.ps1 -CheckOnly通过：Steam会话AppID4348910，账号76561198059528913为现有条目3836818025所有者，公开状态符合预期；未使用SteamCMD重新登录。
- 发布使用publish-workshop.ps1：english上传运行内容、封面、标题/简介及本版更新说明；schinese只更新同一份标题/简介，不重复上传内容。两个SubmitItemUpdate均k_EResultOK，各自随后SetLanguage并禁用缓存核验标题/完整简介，matches=true；回执status=verified，更新时间2026-11-12 00:56:33（香港时间）。
- Steam客户端D:/Steam/logs/workshop_log.txt：00:56:29 Uploaded new content，ManifestID6703503729439725191；00:56:30及00:56:32分别Upload finished for workshop item 3836818025 : OK。
- 公开GetPublishedFileDetails API只读复核：result=1，publishedfileid=3836818025，title=化学助手，visibility=0，hcontent_file=6703503729439725191，time_updated=1794416189，默认description与待传文本逐字一致，7982 UTF-8字节。
- 原始分阶段回执：E:/SteamLibrary/steamapps/common/Probably Stolen/WorkshopUpload/upload-result-0.4.1-languages.json；保留文件哈希、发布前文本、提交/验证结果。
- [工坊页面](https://steamcommunity.com/sharedfiles/filedetails/?id=3836818025)，[english](https://steamcommunity.com/sharedfiles/filedetails/?id=3836818025&l=english)，[schinese](https://steamcommunity.com/sharedfiles/filedetails/?id=3836818025&l=schinese)。核验覆盖服务端内容与文本；未核验网页渲染、订阅下载或实际游戏加载。
- 未部署本地Mods、未修改订阅缓存或存档、未启动游戏。
