package app.xingshu.installer;

import java.util.List;
import org.junit.Test;
import static org.junit.Assert.*;

public class RootInstallerTest {
    @Test
    public void quotesSingleQuotesSafelyForShell() {
        assertEquals("'hello'", RootInstaller.quote("hello"));
        assertEquals("'a'\"'\"'b'", RootInstaller.quote("a'b"));
    }

    @Test
    public void includesBareSuAndStandardPathsWithoutDuplicates() {
        List<String> candidates = RootInstaller.suCandidateCommands();
        assertTrue(candidates.contains("su"));
        assertTrue(candidates.contains("/system/bin/su"));
        assertTrue(candidates.contains("/data/adb/ksu/bin/su"));
        assertEquals(candidates.size(), new java.util.LinkedHashSet<>(candidates).size());
    }

    @Test
    public void validatesRemoteStagingFileNamesStrictly() {
        RootInstaller.validateRemoteFileName("candidate.__data");
        RootInstaller.validateRemoteFileName("core-spec.json");
        RootInstaller.validateRemoteFileName("mod.zip");

        for (String bad : new String[]{"", null, "../evil", "sub/file", "bad name", "semi;id"}) {
            try {
                RootInstaller.validateRemoteFileName(bad);
                fail("Expected IllegalArgumentException for: " + bad);
            } catch (IllegalArgumentException expected) {
                assertTrue(expected.getMessage().contains("非法"));
            }
        }
    }
}
