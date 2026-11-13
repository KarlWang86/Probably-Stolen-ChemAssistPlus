# 进度条 pH 参考色资料 · 2026-11-12

用途：给条形填充选择有来源的指示剂主题色，不改变游戏化学逻辑，不测量或估算游戏混合液pH。配色在CompositionPanel.ComponentIndicatorColor集中定义；两页共用，阶段跟随snapshot.Primary而非按总量最大的溶剂切换。

## 可用参考值及条件

| 游戏成分 | 查到的数据与条件 | 采用颜色与限制 |
| --- | --- | --- |
| caffeine | FUJIFILM Wako产品规格：10g/L、25°C下pH5.5–6.5；Merck SDS另记10g/L、20°C下5.5–6.5 | 黄绿，按约pH6选主题色；不把区间中值当作测量结果。干燥咖啡因与游戏高浓度原液不沿用该实测条件 |
| coffee_mush | Rao与Fuller 2018年原始研究：所测冷/热水咖啡萃取液pH4.85–5.13 | 橙黄，按约pH5选主题色；游戏咖啡浆只是现实咖啡萃取液的近似参照，二者不是同一样品 |
| water | USGS：纯水25°C下pH7.00 | 绿，仅代表纯水参照，不把含杂质/溶解气体/其他成分的混合液当成pH7 |
| acid / base | 游戏只给通用酸液/碱液身份，未明确化学种类及可用于计算的溶液条件 | 保留酸红橙、碱蓝紫分类主题色，不分配具体pH，也不声称强酸/强碱 |
| alcohol | Fisher无水乙醇SDS第9节：pH Not applicable | 灰；不把100%乙醇写成pH7，也不将无水乙醇结论泛化为所有酒精水溶液的测量结果 |

颜色借鉴通用指示剂色谱，具体指示剂配方的颜色并非唯一；填充色为白字可读性调暗，不是数字采样复刻的实验色卡。参考Merck/Supelco 109175通用指示剂pH4–10官方色卡。

## 暂无可用数值的成分

- dream_cap_mush、dream_cap_extract、nightmare_cap_mush、nightmare_extract、fanta、nomore：没有可确认对应的现实化学品身份，使用灰色。
- zyanide：游戏拼写不能据此直接当作特定氰化物或氢氰酸；身份不明，灰色。
- contraceptive：用途类别，不是唯一分子或确定制剂；灰色。
- methylol：仅凭游戏N-Methylol Base名称不能确定具体分子、溶剂和浓度，不借用甲醛/甲醇的数据；灰色。
- lipobond_agent、reducing_agent、stabilizer、water_tablet及其他未列出的成分：功能名称或游戏配方不足以锁定实际化学组成。净水片也不能直接假设为次氯酸钠或某种现实消毒片，使用灰色。
- 未知不等于中性；不再统一使用绿色。未对这些成分分配虚构pH，也未按体积平均pH或由成分纯度换算pH。

## 一手来源

1. [FUJIFILM Wako — Caffeine产品规格](https://labchem-wako.fujifilm.com/europe/product/detail/W01W0103-0679.html)，Property栏目，10g/L、25°C。
2. [Merck — Caffeine 205548 SDS](https://www.merckmillipore.com/Web-MY-Site/en_US/-/USD/ShowDocument-File?Country=MY&Display=inline&DocumentId=205548_SDS_MY_EN.PDF&DocumentType=MSD&DocumentUID=17936612&Language=EN&Origin=null&ProductSKU=EMD_BIO-205548)，第9节，10g/L、20°C。仅交叉核对，不混淆两来源温度。
3. [Rao, N. Z. & Fuller, M. — Acidity and Antioxidant Activity of Cold Brew Coffee, Scientific Reports 8, 16030 (2018)](https://www.nature.com/articles/s41598-018-34392-w)，DOI 10.1038/s41598-018-34392-w；[PMC全文](https://pmc.ncbi.nlm.nih.gov/articles/PMC6207714/)。区间为研究样品测值，不是所有咖啡固定范围。
4. [USGS — National Field Manual, pH, version 2.0](https://pubs.usgs.gov/twri/twri9a6/twri9a64/twri9a_6.4_ver2.0.pdf)，纯水25°C下pH7.00。
5. [Fisher — Ethanol, Anhydrous (Histological) SDS](https://www.fishersci.com/store/msds?countryCode=US&language=en&partNumber=A40520&productDescription=ETHANOL+AHYD+HISTO+20L&vendorId=VN00033897)，2025-12-18修订，第9节pH不适用。
6. [Merck/Supelco — 109175 Universal Indicator官方色卡](https://www.merckmillipore.com/deepweb/assets/sigmaaldrich/product/documents/267/708/ph-109175-karte-su04-col-pronet.pdf)，pH4–10，仅作色系参考。
