# Steam 上传知识来源 · 2026-11-10

来源对话：[尝试上传 Steam 创意工坊 Mod](thread://01a1c121-bf52-73b2-8f9e-19c6db05c1d6?hostId=local)。
本次读取了该对话全部14轮记录；操作摘要维护在 DEVELOPMENT.md，不每轮重读此材料。

- 初次0.3.3上传：用户提供 Committing update...Success，VDF回填3836818025；配置私密。
- 后续0.3.4上传：workshop_log 02:10:27记录 Upload finished for workshop item 3836818025 : OK，console记录Success。
- 回执：游戏根目录 WorkshopUpload/upload-result-0.3.4.json；AppID4348910，内容ManifestID6252409541254687741。
- 上传DLL SHA256：954956BD77D292914091D21BD4C20758D7574F8918154DEB21AC9AF59377B3C6。
- 玩家手动更新后订阅manifest为0.3.4且DLL与回执一致；对话最后一轮02:18附近启动日志确认 source=Workshop、workshop=3836818025、running=True、版本0.3.4。
- 先前下载未更新的原因由玩家确认是未开启自动更新；重订阅未立即解决，手动更新解决。
- 本轮磁盘只读复核：WorkshopUpload/content与订阅DLL均匹配上述SHA256；这是旧上传构建，和当前开发build的B1543432…构建不同。
- 未在本轮重新查询在线页面、执行上传或启动游戏。上述日志加载结论来自该对话当次运行，不保证未来会话状态。
