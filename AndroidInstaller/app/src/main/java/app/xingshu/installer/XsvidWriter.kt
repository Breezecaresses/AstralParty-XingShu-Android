package app.xingshu.installer

import java.io.BufferedInputStream
import java.io.BufferedOutputStream
import java.io.File
import java.io.FileInputStream
import java.io.FileOutputStream
import java.io.IOException
import java.io.RandomAccessFile
import java.nio.ByteBuffer
import java.nio.ByteOrder
import kotlin.math.min
import kotlin.math.roundToInt

/**
 * Writes the XingShu `.xsvid` v1 container (little-endian XSVD header + 12-byte frame index + JPEG frames + optional .m4a audio).
 * Pure JVM implementation so host unit tests can verify exact binary layout without Android hardware.
 */
object XsvidWriter {
    private const val MAGIC = 0x44565358 // "XSVD" little-endian ('X','S','V','D')
    private const val HEADER_BYTES = 40
    private const val MAX_FRAMES_HARD_LIMIT = 3600

    data class TranscodeProfile(
        val maxWidth: Int,
        val maxHeight: Int,
        val targetFps: Double,
        val jpegQuality: Int,
        val maxFrames: Int,
    )

    fun selectAdaptiveProfile(durationSec: Double, fallbackMode: Boolean = false): TranscodeProfile {
        val effectiveSec = durationSec.coerceIn(1.0, 300.0)
        return if (!fallbackMode) {
            when {
                effectiveSec <= 45.0 -> TranscodeProfile(960, 540, 18.0, 80, 3550)
                effectiveSec <= 120.0 -> TranscodeProfile(854, 480, 15.0, 75, 3550)
                effectiveSec <= 240.0 -> TranscodeProfile(768, 432, 12.0, 72, 3550)
                else -> TranscodeProfile(720, 406, min(12.0, 3500.0 / effectiveSec), 70, 3550)
            }
        } else {
            when {
                effectiveSec <= 90.0 -> TranscodeProfile(854, 480, 12.0, 76, 3550)
                effectiveSec <= 180.0 -> TranscodeProfile(768, 432, 10.0, 72, 3550)
                else -> TranscodeProfile(720, 406, min(8.0, 3500.0 / effectiveSec), 70, 3550)
            }
        }
    }

    fun computeCalibratedFpsMilli(frameCount: Int, durationUs: Long, fallbackFpsMilli: Int): Int {
        if (frameCount <= 0 || durationUs < 100_000L) {
            return fallbackFpsMilli.coerceIn(1000, 60000)
        }
        return ((frameCount.toDouble() * 1_000_000_000.0) / durationUs.toDouble())
            .roundToInt()
            .coerceIn(1000, 60000)
    }

    fun writePackage(
        outputFile: File,
        width: Int,
        height: Int,
        fpsMilli: Int,
        frames: List<ByteArray>,
        audioFile: File? = null,
    ) {
        validateHeaderParams(width, height, fpsMilli, frames.size)
        for (i in frames.indices) {
            val len = frames[i].size
            if (len < 16 || len > 4 * 1024 * 1024) {
                throw IllegalArgumentException("第 $i 帧大小无效: $len")
            }
        }
        val audioBytes = readAudioBytesIfValid(audioFile)
        ensureParentDir(outputFile)

        val tmp = File(outputFile.parentFile, outputFile.name + ".tmp")
        try {
            val frameCount = frames.size
            val frameLengths = IntArray(frameCount) { frames[it].size }
            val audioLength = audioBytes?.size ?: 0
            val headerBuf = buildHeaderAndIndex(width, height, fpsMilli, frameLengths, audioLength)

            BufferedOutputStream(FileOutputStream(tmp), 65536).use { out ->
                out.write(headerBuf)
                for (f in frames) {
                    out.write(f)
                }
                if (audioBytes != null && audioBytes.isNotEmpty()) {
                    out.write(audioBytes)
                }
                out.flush()
            }

            finalizeTempFile(tmp, outputFile)
        } catch (t: Throwable) {
            tmp.delete()
            if (t is IOException) throw t
            throw IOException(t.message ?: "写入动态视频包失败", t)
        }
    }

    fun writePackageFromRawStream(
        outputFile: File,
        width: Int,
        height: Int,
        fpsMilli: Int,
        rawFramesFile: File,
        frameLengths: IntArray,
        audioFile: File? = null,
    ) {
        validateHeaderParams(width, height, fpsMilli, frameLengths.size)
        var expectedFrameBytes = 0L
        for (i in frameLengths.indices) {
            val len = frameLengths[i]
            if (len < 16 || len > 4 * 1024 * 1024) {
                throw IllegalArgumentException("第 $i 帧大小无效: $len")
            }
            expectedFrameBytes += len.toLong()
        }
        if (!rawFramesFile.isFile || rawFramesFile.length() != expectedFrameBytes) {
            throw IOException("帧缓存长度不匹配: 预期 $expectedFrameBytes 字节，实际 ${rawFramesFile.length()} 字节")
        }

        val validAudioFile = if (audioFile != null && audioFile.isFile && audioFile.length() > 0L) {
            if (audioFile.length() > 64L * 1024L * 1024L) {
                throw IllegalArgumentException("伴音体积超过 64 MB")
            }
            audioFile
        } else {
            null
        }
        val audioLength = validAudioFile?.length()?.toInt() ?: 0
        ensureParentDir(outputFile)

        val tmp = File(outputFile.parentFile, outputFile.name + ".tmp")
        try {
            val headerBuf = buildHeaderAndIndex(width, height, fpsMilli, frameLengths, audioLength)
            val copyBuf = ByteArray(65536)

            BufferedOutputStream(FileOutputStream(tmp), 65536).use { out ->
                out.write(headerBuf)
                var copiedFrames = 0L
                BufferedInputStream(FileInputStream(rawFramesFile), 65536).use { rawIn ->
                    while (true) {
                        val n = rawIn.read(copyBuf)
                        if (n < 0) break
                        out.write(copyBuf, 0, n)
                        copiedFrames += n.toLong()
                    }
                }
                if (copiedFrames != expectedFrameBytes) {
                    throw IOException("流式写入帧长度异常: $copiedFrames != $expectedFrameBytes")
                }
                if (validAudioFile != null && audioLength > 0) {
                    BufferedInputStream(FileInputStream(validAudioFile), 65536).use { audIn ->
                        while (true) {
                            val n = audIn.read(copyBuf)
                            if (n < 0) break
                            out.write(copyBuf, 0, n)
                        }
                    }
                }
                out.flush()
            }

            finalizeTempFile(tmp, outputFile)
        } catch (t: Throwable) {
            tmp.delete()
            if (t is IOException) throw t
            throw IOException(t.message ?: "流式写入动态视频包失败", t)
        }
    }

    private fun validateHeaderParams(width: Int, height: Int, fpsMilli: Int, frameCount: Int) {
        if (width < 2 || width > 2048 || height < 2 || height > 2048) {
            throw IllegalArgumentException("分辨率超出范围: ${width}x$height")
        }
        if (fpsMilli < 1000 || fpsMilli > 60000) {
            throw IllegalArgumentException("帧率超出范围: $fpsMilli")
        }
        if (frameCount <= 0 || frameCount > MAX_FRAMES_HARD_LIMIT) {
            throw IllegalArgumentException("视频帧数无效: $frameCount")
        }
    }

    private fun readAudioBytesIfValid(audioFile: File?): ByteArray? {
        if (audioFile == null || !audioFile.isFile || audioFile.length() <= 0L) {
            return null
        }
        if (audioFile.length() > 64L * 1024L * 1024L) {
            throw IllegalArgumentException("伴音体积超过 64 MB")
        }
        return FileInputStream(audioFile).use { it.readBytes() }
    }

    private fun ensureParentDir(outputFile: File) {
        val parent = outputFile.parentFile
        if (parent != null && !parent.isDirectory && !parent.mkdirs()) {
            throw IOException("无法创建输出目录: ${parent.absolutePath}")
        }
    }

    private fun buildHeaderAndIndex(
        width: Int,
        height: Int,
        fpsMilli: Int,
        frameLengths: IntArray,
        audioLength: Int,
    ): ByteArray {
        val frameCount = frameLengths.size
        val tableBytes = frameCount * 12
        var cursor = (HEADER_BYTES + tableBytes).toLong()

        val offsets = LongArray(frameCount)
        for (i in 0 until frameCount) {
            offsets[i] = cursor
            cursor += frameLengths[i].toLong()
        }
        val audioOffset = if (audioLength > 0) cursor else 0L

        val headerBuf = ByteBuffer.allocate(HEADER_BYTES + tableBytes).order(ByteOrder.LITTLE_ENDIAN)
        headerBuf.putInt(MAGIC)
        headerBuf.putInt(1)
        headerBuf.putInt(width)
        headerBuf.putInt(height)
        headerBuf.putInt(fpsMilli)
        headerBuf.putInt(frameCount)
        headerBuf.putLong(audioOffset)
        headerBuf.putInt(audioLength)
        headerBuf.putInt(0)

        for (i in 0 until frameCount) {
            headerBuf.putLong(offsets[i])
            headerBuf.putInt(frameLengths[i])
        }
        return headerBuf.array()
    }

    private fun finalizeTempFile(tmp: File, outputFile: File) {
        if (outputFile.exists() && !outputFile.delete()) {
            throw IOException("无法覆盖旧动态包: ${outputFile.name}")
        }
        if (!tmp.renameTo(outputFile)) {
            RandomAccessFile(tmp, "r").use { src ->
                FileOutputStream(outputFile).use { dst ->
                    val buf = ByteArray(65536)
                    while (true) {
                        val n = src.read(buf)
                        if (n < 0) break
                        dst.write(buf, 0, n)
                    }
                    dst.getFD().sync()
                }
            }
            tmp.delete()
        }
    }
}
