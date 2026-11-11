# 0.4.0 工坊双语言发布核验

- 用户明确要求设置提交语言并发布0.4.0；网页删除中文被标题必填拦截，改为保留两个语言并同步同一份文案。
- 使用游戏附带Steamworks.NET及steam_api64.dll编译本地WorkshopPublisher，复用Steam客户端会话；不启动游戏，不下载SDK或改游戏文件。
- 起初查询返回k_EResultFail，客户端connection_log显示22:53:43 Session Replaced；玩家手动恢复在线后只读预检通过。预检证实旧english简介与0.3.9本地一致，旧schinese简介不一致。
- SetItemUpdateLanguage对english、schinese分别使用独立更新句柄。english设置运行内容、封面及标题/简介，提交版本更新说明；schinese只设置标题/简介，SubmitItemUpdate不附加更新说明，不重复上传内容，不修改可见性。
- 2026-11-11 23:04:47（Asia/Hong_Kong）english SubmitItemUpdate返回k_EResultOK，23:04:49 schinese返回k_EResultOK；均不需要接受工坊协议。每次提交后SetLanguage指定对应语言、SetReturnLongDescription(true)、SetAllowCachedResponse(0)，查询标题及完整简介与待传源逐字一致。
- D:/Steam/logs/workshop_log.txt：23:04:46 Uploaded new content ( ManifestID 572067882503914744 )；23:04:47及23:04:49分别有Upload finished for workshop item 3836818025 : OK。未出现第二次内容上传。
- 公开GetPublishedFileDetails API再次核验：result=1，publishedfileid=3836818025，consumer_app_id=4348910，creator=76561198059528913，visibility=0，hcontent_file=572067882503914744，默认description与本地一致。
- 运行文件DLL SHA256：EE286B75742D8AED9DD17E067A2D28D289EA073345218C27AFBAAF790D9A90D1；构建0警告0错误，原生ModScanner PASS，409 methods，capabilities none。未新增/运行测试。
- 简介7990 UTF-8字节，19个记录版本，鸣谢保留。0.3.9待传工作区备份：WorkshopUpload/archive/before-prepare-0.4.0-20261111-230348-903；最终文案准备另备份before-prepare-0.4.0-20261111-230423-582。
- 分阶段原始回执：游戏根目录WorkshopUpload/upload-result-0.4.0-languages.json（含提交前两个语言文本、提交结果、逐字核验结果、内容Manifest、文件哈希及公开API结论），status=verified。
- 页面入口：[默认英文栏](https://steamcommunity.com/sharedfiles/filedetails/?id=3836818025&l=english)、[简体中文栏](https://steamcommunity.com/sharedfiles/filedetails/?id=3836818025&l=schinese)。本次核验的是服务端API文本；网页渲染效果由玩家查看，订阅下载与游戏实际加载尚未核验。
- 未部署本地Mods、未改订阅缓存或存档、未自行启动游戏。
