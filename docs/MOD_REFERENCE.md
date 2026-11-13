# 参考 Mod 缓存

参考：化学一键制作 Chem One-Click Craft，工坊条目 3830072589；本项目独立，无其 DLL 依赖。
本页依据本地历史材料，不将评论或作者说明直接视为已验证行为。

## 鸣谢署名（2026-11-11公开信息核对）

- [化学一键制作 Chem One-Click Craft](https://steamcommunity.com/sharedfiles/filedetails/?id=3830072589)：公开创建者SteamID64为76561198096759167，[主页](https://steamcommunity.com/profiles/76561198096759167/)当前昵称“线上真实”；本地清单署名n1ght，简介并列保留。
- [更好的液体扫描仪 Better Liquid Scanner](https://steamcommunity.com/sharedfiles/filedetails/?id=3831472187)：作者rikuvunua，SteamID64为76561199043886662，[主页](https://steamcommunity.com/profiles/76561199043886662/)。仅参考成分条形布局，无DLL依赖或修改。
- Steam公开ISteamRemoteStorage/GetPublishedFileDetails/v1/返回的creator字段与公开个人主页XML核对，证据索引见Research/WorkshopCredits-20261111.md。昵称可能变化，数字ID为稳定链接。

## 证据版本

- 历史研究包标注 v1.3.9；历史 DLL SHA256：CB4623B78622F869AFBE16122C719C195DA696ABC9FC2708C8BF82905DE8E826。
- 2026-11-10 当前订阅 DLL SHA256：808D473AEAFC396554877FE82602F9C75ADD72343E537192A9EBF26F39E1ABAD；已不同，未重新全量研究，历史结论不自动适用于当前文件。
- 原始比较：Research/原Mod核对与改进记录.txt；完整 IL：Research/original-il.txt、run-il.txt、aux-il.txt。只在研究相关行为时读取。

## 已核实的历史实现与可借鉴点

| 结论来源 | 历史行为 | 本项目取舍 |
| --- | --- | --- |
| 作者/下载说明 | 末端使用净水器 | 历史 RunCore 已无此调用，不能照说明推断 |
| 历史 DLL | TakeLiquid 只搬指定成分，其他水/杂质留源瓶 | 使用原生同比取液，按真实浓度算原溶液体积 |
| 历史 DLL | EnsureAuxMaterials 预加固定2.5mL助剂；PrePurifyAuxSources 预处理多个源瓶 | 本批按需投料，只处理当前工作液 |
| 历史 DLL | 第一只合格滤壶、可能循环最多5遍 | 单道过滤，滤芯优先、棉布备用，各份一次，同类低耐久先用 |
| 历史 DLL | 设备先匹配ID、按温度挑板 | 遍历所有权/占用/真实调温能力，避免第一台误判 |
| 历史 DLL | 主动设温并推进反应/蒸发 | 本项目提取调用原生反应；蒸发交原生过夜 |

评论中的设备、过滤、回收等问题只作为调查线索，不证明当前版本仍有相同问题。
需要复查时将当前 DLL 路径传给 query-api.ps1 -Assembly，重新记录哈希与结论；不修改订阅目录。
