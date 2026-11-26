package app.xingshu.installer

import java.io.BufferedOutputStream
import java.io.File
import java.io.FileInputStream
import java.io.FileOutputStream
import java.io.IOException
import java.io.RandomAccessFile
import java.nio.ByteBuffer
import java.nio.ByteOrder

/**
 * Writes the XingShu `.xsvid` v1 container (little-endian XSVD header + 12-byte frame index + JPEG frames + optional .m4a audio).
 * Pure JVM implementation so host unit tests can verify exact binary layout without Android hardware.
 */
object XsvidWriter {
    private const val MAGIC = 0x44565358 // "XSVD" little-endian ('X','S','V','D')
    private const val HEADER_BYTES = 40

    fun writePackage(
        outputFile: File,
        width: Int,
        height: Int,
        fpsMilli: Int,
        frames: List<ByteArray>,
        audioFile: File? = null,
    ) {
        if (width < 2 || width > 2048 || height < 2 || height > 2048) {
            throw IllegalArgumentException("分辨率超出范围: ${width}x$height")
        }
        if (fpsMilli < 1000 || fpsMilli > 60000) {
            throw IllegalArgumentException("帧率超出范围: $fpsMilli")
        }
        if (frames.isEmpty() || frames.size > 3600) {
            throw IllegalArgumentException("视频帧数无效: ${frames.size}")
        }
        for (i in frames.indices) {
            val len = frames[i].size
            if (len < 16 || len > 4 * 1024 * 1024) {
                throw IllegalArgumentException("第 $i 帧大小无效: $len")
            }
        }

        val audioBytes = if (audioFile != null && audioFile.isFile && audioFile.length() > 0L) {
            if (audioFile.length() > 64L * 1024L * 1024L) {
                throw IllegalArgumentException("伴音体积超过 64 MB")
            }
            FileInputStream(audioFile).use { it.readBytes() }
        } else {
            null
        }

        val parent = outputFile.parentFile
        if (parent != null && !parent.isDirectory && !parent.mkdirs()) {
            throw IOException("无法创建输出目录: ${parent.absolutePath}")
        }

        val tmp = File(outputFile.parentFile, outputFile.name + ".tmp")
        try {
            val frameCount = frames.size
            val tableBytes = frameCount * 12
            var cursor = (HEADER_BYTES + tableBytes).toLong()

            val offsets = LongArray(frameCount)
            for (i in 0 until frameCount) {
                offsets[i] = cursor
                cursor += frames[i].size.toLong()
            }
            val audioOffset = if (audioBytes != null && audioBytes.isNotEmpty()) cursor else 0L
            val audioLength = audioBytes?.size ?: 0

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
                headerBuf.putInt(frames[i].size)
            }

            BufferedOutputStream(FileOutputStream(tmp), 65536).use { out ->
                out.write(headerBuf.array())
                for (f in frames) {
                    out.write(f)
                }
                if (audioBytes != null && audioBytes.isNotEmpty()) {
                    out.write(audioBytes)
                }
                out.flush()
            }

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
        } catch (t: Throwable) {
            tmp.delete()
            if (t is IOException) throw t
            throw IOException(t.message ?: "写入动态视频包失败", t)
        }
    }
}
