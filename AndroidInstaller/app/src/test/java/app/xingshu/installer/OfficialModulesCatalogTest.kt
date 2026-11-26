package app.xingshu.installer

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class OfficialModulesCatalogTest {
    @Test
    fun allOfficialModulesMatchExpectedIdsAndSafeFormat() {
        assertEquals(16, OfficialModulesCatalog.ALL.size)
        val expected = listOf(
            "hand-monitor",
            "battle-stats",
            "auto-social",
            "defense-hint",
            "knockback-preview",
            "monster-action",
            "free-camera",
            "game-speed",
            "thinking-time",
            "action-guard",
            "replay-player",
            "static-portraits",
            "select-map-name",
            "player-star-buffs",
            "megath-damage",
            "player-combat-hud",
        )
        assertEquals(expected, OfficialModulesCatalog.ALL.map { it.id })
        assertEquals(16, OfficialModulesCatalog.ALL.map { it.id }.distinct().size)
        OfficialModulesCatalog.ALL.forEach { mod ->
            assertTrue("Unsafe id: ${mod.id}", OfficialModulesCatalog.isSafeId(mod.id))
            assertTrue(mod.name.isNotBlank())
            assertTrue(mod.description.isNotBlank())
            assertTrue(OfficialModulesCatalog.GROUPS.contains(mod.group))
        }
    }

    @Test
    fun csvSerializerDeduplicatesAndRejectsTraversal() {
        val fullCsv = OfficialModulesCatalog.allIdsCsv()
        assertEquals(16, fullCsv.split(",").size)
        assertEquals(
            "hand-monitor,game-speed",
            OfficialModulesCatalog.toSafeCsv(listOf(" hand-monitor ", "../evil", "HAND-MONITOR", "game-speed")),
        )
        assertFalse(OfficialModulesCatalog.isSafeId("../bad"))
        assertFalse(OfficialModulesCatalog.isSafeId("a"))
    }
}
