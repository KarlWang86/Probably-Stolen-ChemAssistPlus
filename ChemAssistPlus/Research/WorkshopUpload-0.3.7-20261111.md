# 0.3.7上传与简介核验 · 2026-11-11

- 19:44登录后No Connection，未提交；19:45开始准备后反复断线，该SteamCMD辅助进程已结束。
- 19:46启用-tcp，上传内容ManifestID4624332752583670042，但最终Invalid Parameter，没有成功提交。
- 官方ISteamRemoteStorage文档定义k_cchPublishedDocumentDescriptionMax=8000字节。原已确认简介为14277 UTF-8字节，超限；本地完整CHANGELOG未删改，原完整版简介/模板已归档，工坊稿精简为7524字节且保留全部16版本、用法功能及末尾鸣谢。
- 19:49:16最终Success，workshop_log同条目OK，复用内容ManifestID4624332752583670042。公开API验证hcontent_file相同、visibility=0、默认简介与本地一致。
- DLL SHA256为80F51B95EF68031983EA8602A7660DDAC28411D064DD1DB5B6FBEC568B7090B6；三个运行文件与已扫描build一致。回执WorkshopUpload/upload-result-0.3.7.json。
- 简体中文页面仍有独立旧0.3.6描述，默认english描述已经0.3.7。2026-11-11玩家明确要求删除独立中文简介，暂时只保留一份默认简介，以后再做多语言。当前IAB已打开Steam登录页，需玩家登录后检查编辑界面能否删除/回退；尚未删除。
- 先前Windows浏览器控制因不能可靠确定Chrome当前URL停止；本轮使用受支持的IAB标签，不复用该窗口控制。Steam网页登录由玩家本人完成，不读取密码/验证码。

官方长度依据：https://partner.steamgames.com/doc/api/ISteamRemoteStorage#k_cchPublishedDocumentDescriptionMax
