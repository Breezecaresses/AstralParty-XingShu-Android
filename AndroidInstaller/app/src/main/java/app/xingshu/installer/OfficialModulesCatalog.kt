package app.xingshu.installer

/**
 * Canonical catalog of the 16 modular official XingShu features that map 1:1 to
 * `xingshu/plugins/xingshu.launcher.<id>.module` markers inside the Android game data folder.
 */
object OfficialModulesCatalog {
    data class OfficialModule(
        val id: String,
        val name: String,
        val description: String,
        val group: String,
    )

    val ALL: List<OfficialModule> = listOf(
        OfficialModule("hand-monitor", "手牌监视", "局内手牌入口与已授权合作局队友同步", "对局信息"),
        OfficialModule("battle-stats", "对局统计", "局内统计挂件与对局数据", "对局信息"),
        OfficialModule("auto-social", "自动化社交", "整合局内快捷致谢、头像自动点赞、每日星币商店与结算自动截图", "快捷与娱乐"),
        OfficialModule("defense-hint", "防御与卡牌点数提示", "本人防御/闪避所需点数参考，以及战斗卡牌打出后点数与加成明细", "战术推演"),
        OfficialModule("knockback-preview", "击飞预测", "局内击飞概率参考", "战术推演"),
        OfficialModule("monster-action", "怪物行动可视", "地图上实时显示怪物10格索敌推演、投掷点数、移动路径选择与主动技能冷却", "战术推演"),
        OfficialModule("free-camera", "自由镜头", "镜头移动、缩放与地图查看", "操作与画面"),
        OfficialModule("game-speed", "游戏变速", "独立的游戏内变速设置", "操作与画面"),
        OfficialModule("thinking-time", "思考时间", "局内本地计时显示调整", "操作与画面"),
        OfficialModule("action-guard", "战局守护", "本地战局状态监视与重连设置", "对局信息"),
        OfficialModule("replay-player", "局内录像", "游戏内回放播放面板", "对局信息"),
        OfficialModule("static-portraits", "立绘替换", "独立的立绘与动态资源显示设置", "操作与画面"),
        OfficialModule("select-map-name", "选人地图提示", "在选人界面右侧显示当前地图名称", "战术推演"),
        OfficialModule("player-star-buffs", "角色Buff显示", "在左侧角色头像⭐区域显示疾行与角色专属 Buff 图标，支持分类筛选", "对局信息"),
        OfficialModule("megath-damage", "梅加斯伤害计算", "游玩梅加斯时显示主动技能理论最大伤害面板（可拖动调节位置）", "战术推演"),
        OfficialModule("player-combat-hud", "角色状态头顶显示", "在棋盘玩家角色上方显示技能冷却、血量、攻防与最终移速（可调节位置）", "对局信息"),
    )

    val GROUPS: List<String> = listOf("全部", "对局信息", "战术推演", "操作与画面", "快捷与娱乐")

    val CANONICAL_IDS: Set<String> = ALL.map { it.id }.toSet()

    val RETIRED_IDS: Set<String> = setOf(
        "monster-targeting",
        "monster-cooldown",
        "battle-card-points",
        "card-points",
        "quick-thanks",
        "avatar-praise",
        "star-coin-shop",
        "settlement-capture",
        "settlement-screenshot",
        "custom-lobby-bg",
    )

    private val SAFE_ID = Regex("^[a-z0-9-]{2,48}$")

    fun isSafeId(id: String): Boolean = SAFE_ID.matches(id)

    fun allIdsCsv(): String = ALL.joinToString(",") { it.id }

    fun toSafeCsv(ids: Collection<String>): String =
        ids.map { it.trim().lowercase() }.filter { isSafeId(it) }.distinct().joinToString(",")
}
