package app.xingshu.installer

import android.content.Intent
import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.activity.result.contract.ActivityResultContracts

class MainActivity : ComponentActivity() {
    private lateinit var controller: AppController

    private val pickLocalArchive = registerForActivityResult(
        ActivityResultContracts.OpenDocument()
    ) { uri ->
        if (uri != null) controller.installLocalArchiveUri(uri)
    }

    private val pickWallpaperImage = registerForActivityResult(
        ActivityResultContracts.OpenDocument()
    ) { uri ->
        if (uri != null) controller.importWallpaperUri(uri)
    }

    private val pickLobbyBgImage = registerForActivityResult(
        ActivityResultContracts.OpenDocument()
    ) { uri ->
        if (uri != null) controller.importLobbyBgMediaUri(uri, isVideo = false)
    }

    private val pickLobbyBgVideo = registerForActivityResult(
        ActivityResultContracts.OpenDocument()
    ) { uri ->
        if (uri != null) controller.importLobbyBgMediaUri(uri, isVideo = true)
    }

    private val pickCustomFontFile = registerForActivityResult(
        ActivityResultContracts.OpenDocument()
    ) { uri ->
        if (uri != null) controller.importCustomFontUri(uri)
    }

    private val exportLogFile = registerForActivityResult(
        ActivityResultContracts.CreateDocument("text/plain")
    ) { uri ->
        if (uri != null) controller.writeDiagnosticLogToUri(uri)
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        enableEdgeToEdge()
        super.onCreate(savedInstanceState)

        controller = AppController(this).apply {
            onPickLocalArchiveRequest = {
                pickLocalArchive.launch(arrayOf("application/zip", "application/x-zip-compressed", "application/octet-stream", "*/*"))
            }
            onPickWallpaperRequest = {
                pickWallpaperImage.launch(arrayOf("image/*"))
            }
            onPickLobbyBgImageRequest = {
                pickLobbyBgImage.launch(arrayOf("image/*"))
            }
            onPickLobbyBgVideoRequest = {
                pickLobbyBgVideo.launch(arrayOf("video/*", "application/octet-stream", "*/*"))
            }
            onPickCustomFontRequest = {
                pickCustomFontFile.launch(arrayOf("font/*", "application/x-font-ttf", "application/octet-stream", "*/*"))
            }
            onExportLogRequest = { defaultName ->
                exportLogFile.launch(defaultName)
            }
        }
        controller.initialize(intent)

        setContent {
            XingShuMobileApp(controller = controller)
        }
    }

    override fun onStart() {
        super.onStart()
        controller.onStart()
    }

    override fun onResume() {
        super.onResume()
        controller.onResume()
    }

    override fun onStop() {
        controller.onStop()
        super.onStop()
    }

    override fun onNewIntent(intent: Intent) {
        super.onNewIntent(intent)
        setIntent(intent)
        controller.handleIntent(intent)
    }

    override fun onDestroy() {
        controller.dispose()
        super.onDestroy()
    }
}
