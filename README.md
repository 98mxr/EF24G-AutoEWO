# EF-24G Auto EWO

Automatic electronic warfare officer and SEAD assistant for the **EF-24G Mischief** in VTOL VR.

面向《VTOL VR》电子战机 **EF-24G Mischief** 的自动电子战官与防空压制辅助模组。

Players can subscribe on the Steam Workshop for VTOL VR Mod Loader. This repository is the source. The Workshop item is published with **VTOL VR Mod Uploader** and will be linked here once the public item URL is available.

可在 VTOL VR Mod Loader 的 Steam 创意工坊订阅。本仓库是源码。创意工坊物品通过 **VTOL VR Mod Uploader** 发布；公开页面地址确定后会写在这里。

Workshop: https://steamcommunity.com/app/3018410/workshop/

## Credits / 署名

- 者也公爵
- Grok 4.7
- Gemini 4 Argon

## What it does / 功能

The mod attaches to an aircraft that has an `ElectronicWarfareController` (the EF-24G). It does not scan or jam by itself until you open the panel and leave **OFF**, except for the independent toggles below.

模组只接管带电子战控制器的载机（EF-24G）。干扰模式默认为 **OFF**。下面单独列出的开关有各自的默认值。

- **Jamming:** defensive silence, or offensive assignment across search, track, and lock. Threat score, terrain masking, same-band beam clustering, RWR preemption, and terminal home-on-jam handling.
- **CMS:** chaff for radar missiles and flares for infrared missiles.
- **TGP:** optional slew, line-of-sight check, and GPS write for emitters that are not yet stored.
- **Anti-radiation:** dynamic launch zone handoff, friendly-missile deconfliction, semi-auto or full-auto.
- **ADM-160:** one button writes a route toward the densest hostile radar cluster and enables an SAS identity when the round provides one.

- **干扰：** 自卫静默，或对搜索、跟踪、锁定主动分配通道。含威胁评分、地形遮蔽、同频波束聚类、RWR 抢占和末端防干扰寻的。
- **对抗：** 雷达弹抛箔条，红外弹抛热焰弹。
- **光电吊舱：** 可选转向、视线检查，并把尚未入库的辐射源写入 GPS。
- **反辐射：** 动态包线交接、友弹冲突避开、半自动或全自动。
- **ADM-160：** 一个按钮把航路写到敌方雷达最密区域，并在诱饵弹提供型号时打开 SAS。

The panel is an IMGUI window on the flat game view. Use the mouse there. The default key is **F8**. Drag the title bar to move the window.

面板画在游戏的平面窗口上，用鼠标操作。默认快捷键是 **F8**。拖动标题栏可以移动窗口。

## Requirements / 环境

- VTOL VR with the EF-24G Mischief DLC
- [VTOL VR Mod Loader](https://steamcommunity.com/app/3018410)
- Build machine: .NET SDK 8 and the game installed, because the project references `VTOLVR_Data\Managed` and `VTOL VR Mod Loader\Managed`

- 已安装 EF-24G Mischief DLC 的《VTOL VR》
- [VTOL VR Mod Loader](https://steamcommunity.com/app/3018410)
- 自行编译需要 .NET SDK 8，以及本机已安装游戏。工程引用 `VTOLVR_Data\Managed` 和 `VTOL VR Mod Loader\Managed`

## Install / 安装

**Workshop / 创意工坊**

Subscribe in the Mod Loader workshop, then load the item in VTOL VR Mod Loader.

在 Mod Loader 创意工坊订阅后，于 Mod Loader 中加载。

**Release zip / 发行压缩包**

Download `EF24G-AutoEWO.zip` from the GitHub Release. Extract it so this path exists:

从 GitHub Release 下载 `EF24G-AutoEWO.zip`，解压后路径应为：

`VTOL VR/@Mod Loader/Mods/EF24G-AutoEWO/EF24GAutoEWO.dll`

`item.json` must sit next to the DLL. / `item.json` 必须和 DLL 放在同一文件夹。

## Build / 编译

```powershell
dotnet build EF24GAutoEWO.csproj -c Release
```

A successful build also copies `EF24GAutoEWO.dll` and `item.json` into `@Mod Loader/Mods/EF24G-AutoEWO`.

编译成功后会把 `EF24GAutoEWO.dll` 和 `item.json` 复制到 `@Mod Loader/Mods/EF24G-AutoEWO`。

## Panel reference / 面板说明

Current selection is highlighted. Changed values apply immediately. **保存设置 / Save settings** writes them to the Mod Loader settings file. Unloading the mod also saves.

当前选中的按钮会高亮。改动立即生效。**保存设置** 会写入 Mod Loader 的设置文件。卸载模组时也会保存。

### Operating mode / 工作模式

OFF only stops jammer automation. CMS, TGP, and anti-radiation follow their own toggles.

OFF 只停止干扰自动化。箔条热焰弹、光电吊舱和反辐射仍由各自的开关决定。

| Control / 控件 | 中文 | English |
| --- | --- | --- |
| OFF | 不自动开关干扰。已经由本模组打开的干扰会被释放。你手动占用的通道保持原样。 | Do not automate jamming. Jams opened by this mod are released. Channels you took manually stay as they are. |
| DEFENSIVE | 平时电磁静默。只有本机被火控锁定，或雷达正在制导导弹时，才对那个威胁打开 DRFM。威胁消失后关掉。 | Stay electromagnetically quiet. Open DRFM against a threat only while it has a fire-control lock on you or is guiding a missile. Stop when that threat is gone. |
| OFFENSIVE | 对搜索、跟踪和锁定辐射源分配干扰通道。 | Assign jamming channels to search, track, and locked emitters. |

### Target priority / 目标优先级

| Control / 控件 | 中文 | English |
| --- | --- | --- |
| SAM 优先 | 地面和装甲单位上的雷达，威胁分乘 2。 | Radars on ground or armored units score twice as high. |
| 空中优先 | 空中单位上的雷达，威胁分乘 2。 | Radars on aircraft score twice as high. |
| 均衡 | 只按威胁等级、距离和信号强度排序，不给某一类额外权重。 | Rank only by threat state, range, and signal strength. No category weight. |

### Band authorization / 频段授权

The aircraft still has to be able to transmit on that band. A forbidden band is never assigned.

载机本身也必须能在该频段发射。被关掉的频段绝不会被分配。

| Control / 控件 | 中文 | English |
| --- | --- | --- |
| 允许 Low | 允许自动使用低段（约 0.1–3）。 | Allow automatic use of the low band (about 0.1–3). |
| 允许 Mid | 允许自动使用中段（约 3–8）。 | Allow automatic use of the mid band (about 3–8). |
| 允许 High | 允许自动使用高段（约 8–18）。 | Allow automatic use of the high band (about 8–18). |

### Sliders / 滑条

| Control / 控件 | Range / 范围 | Default / 默认 | 中文 | English |
| --- | --- | --- | --- | --- |
| 功率上限 | 50–100% | 100 | 每个干扰吊舱最多使用自身最大功率的这一百分比。你已锁定的通道先占用额度。 | Each jammer pod may use at most this percentage of its own maximum power. Channels you locked consume budget first. |
| 电量保护阈值 | 5–80% | 25 | 电量百分比低于此值，且发动机与 APU 的充电补不上干扰耗电时，关掉针对搜索雷达的自动干扰。锁定和导弹制导通道保留。 | When charge is below this percentage and engine plus APU charging cannot cover jammer drain, drop automatic search jamming. Lock and missile-guidance channels stay. |
| 同频簇夹角 | 2–30° | 8 | 同一频段、相对本机夹角小于该值的目标合成一簇，只分配一条通道指向簇中心附近威胁最高的目标。 | Same-band targets whose angle from your aircraft is below this value form one cluster. One channel points at the highest threat near the cluster center. |
| 末端防 HOJ 距离 | 400–12000 m | 2000 | 敌方雷达制导导弹进入此距离并仍在接近时，自动噪声通道改为 DRFM，不能改的就关掉，并按脉冲间隔抛箔条。 | When a hostile radar-guided missile is inside this range and still closing, automatic noise channels switch to DRFM, or stop if they cannot. Chaff is released on the pulse interval. |
| CMS 危险距离 | 500–20000 m | 3500 | 正在接近的敌方雷达弹进入此距离时抛箔条；红外弹进入此距离时抛热焰弹。 | Dispense chaff for a closing hostile radar missile inside this range, and flares for an infrared missile inside this range. |
| CMS 脉冲间隔 | 0.1–3 s | 0.4 | 两次箔条或两次热焰弹之间的最短时间。箔条和热焰弹各自计时。 | Minimum time between chaff releases, and separately between flare releases. |
| 发射冷却 | 0.4–15 s | 2 | 全自动反辐射弹两发之间的最短间隔。 | Minimum time between full-auto anti-radiation shots. |

### Toggles / 开关

| Control / 控件 | Default / 默认 | 中文 | English |
| --- | --- | --- | --- |
| 自动箔条 / 热焰弹 | 开 | 按上表距离自动抛放。末端防 HOJ 的箔条在干扰模式不是 OFF 时仍会联动，即使关掉本开关。 | Dispense automatically at the ranges above. Terminal home-on-jam chaff still runs while jamming mode is not OFF, even if this toggle is off. |
| TGP 自动轮询并写入 GPS | 关 | 你约 4 秒内没有手动转动吊舱，且吊舱没有锁住别的目标时，转向高威胁辐射源，确认视线后锁定并写入 GPS 组 `EWO 1`，然后解锁看下一个。 | When you have not slewed the pod for about 4 seconds and it is not holding some other lock, it slews to a high threat, checks line of sight, locks, writes GPS group `EWO 1`, then unlocks and moves on. |
| 反辐射交接 | 开 | 在挂载的反辐射弹里挑选进入包线、且没有友方反辐射弹正在攻击的辐射源。只有当前已经选中反辐射弹，或正在全自动发射时，才会改写 TSD 选中目标。 | Pick an in-envelope emitter that no friendly anti-radiation missile is already attacking. The TSD selection changes only when an anti-radiation weapon is already selected, or when full-auto is firing. |
| 允许全自动发射（仍需要主臂） | 关 | 与 **FULL** 同时开启，并且飞机主臂已经打开、导引头允许发射时，模组才会选中该武器并打一发。 | Together with **FULL**, and only while the aircraft master arm is on and launch is authorized, the mod selects that weapon and fires one round. |
| 显示简洁 HUD | 开 | 面板关闭时，在画面左上角显示模式、电量、干扰、三角定位航向、反辐射和吊舱状态。 | While the panel is closed, show mode, battery, jamming, triangulation heading, anti-radiation, and pod status at the top left. |

### Anti-radiation launch mode / 反辐射发射方式

| Control / 控件 | 中文 | English |
| --- | --- | --- |
| SEMI 半自动 | 完成目标选择。武器已选中且导引头允许时，提示你扣扳机。模组不发射。 | Select the target. When the weapon is selected and the seeker authorizes launch, you are told to pull the trigger. The mod does not fire. |
| FULL 全自动 | 还要打开「允许全自动发射」和飞机主臂。进入包线后自动打一发，然后进入冷却。 | Also requires Allow full-auto fire and the aircraft master arm. Inside the envelope it fires one round, then waits out the cooldown. |

### Channels and buttons / 通道与按钮

| Control / 控件 | 中文 | English |
| --- | --- | --- |
| 通道勾选 | 勾选后该干扰通道标记为手动，自动逻辑不再改它的目标、模式、频段或功率。取消勾选后恢复自动。游戏自己因目标丢失、视距或电量停发，不会把通道变成手动。 | A checked channel is manual. Automation will not change its target, mode, band, or power. Clear the check to return it to automation. The game stopping a jam because the target was lost, left the beam, or the battery failed does not mark the channel manual. |
| 清除手动锁 | 取消所有通道的手动锁定。 | Clear the manual lock on every channel. |
| 配置 ADM-160 | 不发射。把 `MALD 1` 航路写到敌方防空雷达最密的一簇，并写到每枚未发射的 ADM-160。若该弹提供了可用的 SAS 型号，就设为诱饵反射特征。 | Does not fire. Writes the `MALD 1` route to the densest hostile air-defense radar cluster and assigns it to every unfired ADM-160. If a round offers a usable SAS identity, that identity is selected. |
| 保存设置 | 把当前面板写入 Mod Loader 设置文件。 | Write the current panel to the Mod Loader settings file. |
| F8 / F9 / F10 / Ins / Home | 选择打开或关闭本面板的按键。高亮的是当前按键。 | Choose the key that opens and closes this panel. The highlighted key is the current one. |

### Status lines / 状态行

| Line / 行 | 中文 | English |
| --- | --- | --- |
| 顶部状态 | 是否已接管当前载机，以及最近一次保存是否成功。 | Whether the current aircraft is attached, and whether the last save succeeded. |
| 干扰行 | 当前簇数量、抢占、电力保护或静默原因。 | Cluster count, preemption, battery safeguard, or why the system is quiet. |
| 三角定位 | 对尚未收敛的最高威胁，显示离当前机头更近的垂直航向。 | For the strongest emitter that has not converged, shows the perpendicular heading closer to the nose. |
| 反辐射 / 吊舱 / 诱饵结果 | 包线、等待主臂、GPS 写入或 ADM-160 配置结果。 | Envelope, waiting for master arm, GPS write, or ADM-160 setup result. |

## Workshop thumbnail / 创意工坊缩略图

VTOL VR Mod Uploader does not publish a required pixel size. Steam rejects preview files larger than **1 MB**. The in-game mod card and the Workshop browse tile crop toward a square, so use:

官方上传工具没有规定像素尺寸。Steam 拒绝超过 **1 MB** 的预览图。游戏内模组卡片和创意工坊浏览格会按正方形裁切，因此建议：

| Item / 项目 | Requirement / 要求 |
| --- | --- |
| Aspect / 横纵比 | **1:1**. 16:9 is cropped in the mod list. / **1:1**。16:9 会在模组列表里被裁切。 |
| Pixels / 像素 | **1024×1024** recommended. Minimum about **195×195**. Do not exceed **2048×2048**. / 推荐 **1024×1024**。最小约 **195×195**。不要超过 **2048×2048**。 |
| Format / 格式 | PNG or JPG. PNG if the art has text or flat color. / PNG 或 JPG。有文字或纯色时用 PNG。 |
| Size / 体积 | **1 MB** hard maximum. Prefer under **500 KB**. / 硬限制 **1 MB**。最好小于 **500 KB**。 |
| Color / 色彩 | sRGB. Keep important art inside the center 80%, because the card corners are cropped. / sRGB。重要内容放在中央 80%，卡片四角会被裁掉。 |
| Text / 文字 | Large short title only, such as `EF-24G Auto EWO`. Tiny body text will not be readable. / 只放短标题，例如 `EF-24G Auto EWO`。小字无法阅读。 |

## License / 许可证

MIT. Copyright (c) 2026 者也公爵. See [LICENSE](LICENSE).

MIT。版权所有 (c) 2026 者也公爵。见 [LICENSE](LICENSE)。
