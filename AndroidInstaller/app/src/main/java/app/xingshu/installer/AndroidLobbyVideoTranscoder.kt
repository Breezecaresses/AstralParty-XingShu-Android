package app.xingshu.installer

import android.graphics.Bitmap
import android.graphics.ImageFormat
import android.graphics.Rect
import android.graphics.YuvImage
import android.media.Image
import android.media.MediaCodec
import android.media.MediaCodecInfo
import android.media.MediaExtractor
import android.media.MediaFormat
import android.media.MediaMetadataRetriever
import android.media.MediaMuxer
import android.os.Build
import java.io.ByteArrayOutputStream
import java.io.File
import java.io.FileInputStream
import java.io.IOException
import java.nio.ByteBuffer
import kotlin.math.min
import kotlin.math.roundToInt

internal object AndroidLobbyVideoTranscoder {
    private const val MAX_WIDTH = 960
    private const val MAX_HEIGHT = 540
    private const val TARGET_FPS = 18
    private const val MAX_DURATION_US = 20_000_000L
    private const val MAX_FRAMES = 360
    private const val JPEG_QUALITY = 80

    data class TranscodeResult(
        val width: Int,
        val height: Int,
        val fpsMilli: Int,
        val frameCount: Int,
        val hasAudio: Boolean,
        val outputSizeBytes: Long,
    )

    fun isTranscodableVideoFile(fileName: String): Boolean {
        val dot = fileName.lastIndexOf('.')
        if (dot < 0) return false
        return when (fileName.substring(dot).lowercase()) {
            ".mp4", ".mov", ".webm", ".m4v", ".mkv" -> true
            else -> false
        }
    }

    fun transcodeVideoToXsvid(
        inputMp4: File,
        outputXsvid: File,
        onProgress: (Int, String) -> Unit,
    ): TranscodeResult {
        if (!inputMp4.isFile || inputMp4.length() <= 0L) {
            throw IOException("待转码视频文件无效")
        }
        onProgress(2, "正在分析视频流与音轨…")
        val tempAudio = File(outputXsvid.parentFile, outputXsvid.name + ".audio.m4a")
        val hasAudio = tryExtractAudioTrack(inputMp4, tempAudio)

        try {
            var targetWidth = 0
            var targetHeight = 0
            var actualFpsMilli = TARGET_FPS * 1000
            val frames = ArrayList<ByteArray>()

            val hwOk = try {
                decodeViaMediaCodec(inputMp4, frames, onProgress) { w, h, fpsMilli ->
                    targetWidth = w
                    targetHeight = h
                    actualFpsMilli = fpsMilli
                }
                frames.size >= 2 && targetWidth >= 2 && targetHeight >= 2
            } catch (_: Throwable) {
                false
            }

            if (!hwOk) {
                frames.clear()
                onProgress(15, "正在切换兼容解码提取视频帧…")
                val fallback = decodeViaMetadataRetriever(inputMp4, frames, onProgress)
                targetWidth = fallback.first
                targetHeight = fallback.second
                actualFpsMilli = fallback.third
            }

            if (frames.isEmpty() || targetWidth < 2 || targetHeight < 2) {
                throw IOException("未能从视频中解码出有效画面，请确认视频为 H.264 / H.265 MP4")
            }

            onProgress(94, "正在封装手机动态背景（${frames.size} 帧）…")
            XsvidWriter.writePackage(
                outputFile = outputXsvid,
                width = targetWidth,
                height = targetHeight,
                fpsMilli = actualFpsMilli,
                frames = frames,
                audioFile = if (hasAudio && tempAudio.isFile && tempAudio.length() > 0L) tempAudio else null,
            )
            onProgress(100, "转码完成")
            return TranscodeResult(
                width = targetWidth,
                height = targetHeight,
                fpsMilli = actualFpsMilli,
                frameCount = frames.size,
                hasAudio = hasAudio && tempAudio.isFile && tempAudio.length() > 0L,
                outputSizeBytes = outputXsvid.length(),
            )
        } finally {
            tempAudio.delete()
        }
    }

    private fun computeScaledSize(srcW: Int, srcH: Int): Pair<Int, Int> {
        val safeW = srcW.coerceAtLeast(2)
        val safeH = srcH.coerceAtLeast(2)
        val scale = min(1.0, min(MAX_WIDTH.toDouble() / safeW.toDouble(), MAX_HEIGHT.toDouble() / safeH.toDouble()))
        val outW = ((safeW * scale).roundToInt() and 1.inv()).coerceIn(2, MAX_WIDTH)
        val outH = ((safeH * scale).roundToInt() and 1.inv()).coerceIn(2, MAX_HEIGHT)
        return outW to outH
    }

    private fun decodeViaMediaCodec(
        inputFile: File,
        outFrames: MutableList<ByteArray>,
        onProgress: (Int, String) -> Unit,
        onMeta: (Int, Int, Int) -> Unit,
    ) {
        val extractor = MediaExtractor()
        var codec: MediaCodec? = null
        var fis: FileInputStream? = null
        try {
            fis = FileInputStream(inputFile)
            extractor.setDataSource(fis.fd)
            var videoTrackIndex = -1
            var videoFormat: MediaFormat? = null
            for (i in 0 until extractor.trackCount) {
                val fmt = extractor.getTrackFormat(i)
                val mime = fmt.getString(MediaFormat.KEY_MIME) ?: ""
                if (mime.startsWith("video/")) {
                    videoTrackIndex = i
                    videoFormat = fmt
                    break
                }
            }
            if (videoTrackIndex < 0 || videoFormat == null) {
                throw IOException("未找到视频轨道")
            }

            extractor.selectTrack(videoTrackIndex)
            val mime = videoFormat.getString(MediaFormat.KEY_MIME) ?: throw IOException("视频编码未知")
            val rawW = if (videoFormat.containsKey(MediaFormat.KEY_WIDTH)) videoFormat.getInteger(MediaFormat.KEY_WIDTH) else 1280
            val rawH = if (videoFormat.containsKey(MediaFormat.KEY_HEIGHT)) videoFormat.getInteger(MediaFormat.KEY_HEIGHT) else 720
            val durationUs = if (videoFormat.containsKey(MediaFormat.KEY_DURATION)) {
                videoFormat.getLong(MediaFormat.KEY_DURATION).coerceIn(1_000_000L, MAX_DURATION_US)
            } else {
                12_000_000L
            }
            val (outW, outH) = computeScaledSize(rawW, rawH)
            onMeta(outW, outH, TARGET_FPS * 1000)

            videoFormat.setInteger(MediaFormat.KEY_COLOR_FORMAT, MediaCodecInfo.CodecCapabilities.COLOR_FormatYUV420Flexible)
            codec = MediaCodec.createDecoderByType(mime)
            codec.configure(videoFormat, null, null, 0)
            codec.start()

            val info = MediaCodec.BufferInfo()
            val stepUs = 1_000_000L / TARGET_FPS
            var nextSamplePtsUs = 0L
            var sawInputEos = false
            var sawOutputEos = false
            val baos = ByteArrayOutputStream(65536)

            while (!sawOutputEos && outFrames.size < MAX_FRAMES) {
                if (!sawInputEos) {
                    val inIdx = codec.dequeueInputBuffer(10_000L)
                    if (inIdx >= 0) {
                        val inBuf = codec.getInputBuffer(inIdx)
                        if (inBuf != null) {
                            val sampleSize = extractor.readSampleData(inBuf, 0)
                            val samplePts = extractor.sampleTime
                            if (sampleSize < 0 || samplePts > MAX_DURATION_US + 500_000L) {
                                codec.queueInputBuffer(inIdx, 0, 0, 0L, MediaCodec.BUFFER_FLAG_END_OF_STREAM)
                                sawInputEos = true
                            } else {
                                codec.queueInputBuffer(inIdx, 0, sampleSize, samplePts, 0)
                                extractor.advance()
                            }
                        }
                    }
                }

                val outIdx = codec.dequeueOutputBuffer(info, 15_000L)
                if (outIdx >= 0) {
                    var image: Image? = null
                    try {
                        if ((info.flags and MediaCodec.BUFFER_FLAG_END_OF_STREAM) != 0) {
                            sawOutputEos = true
                        }
                        if (info.size > 0 && info.presentationTimeUs >= nextSamplePtsUs && info.presentationTimeUs <= MAX_DURATION_US) {
                            image = codec.getOutputImage(outIdx)
                            if (image != null) {
                                val jpeg = encodeYuvImageScaledJpeg(image, outW, outH, baos)
                                outFrames.add(jpeg)
                                nextSamplePtsUs += stepUs
                                val pct = (10 + (info.presentationTimeUs.toDouble() / durationUs.toDouble()) * 80.0)
                                    .roundToInt()
                                    .coerceIn(10, 90)
                                if (outFrames.size == 1 || outFrames.size % 12 == 0) {
                                    onProgress(pct, "正在硬件转码视频帧 (${outFrames.size} 帧)…")
                                }
                            }
                        }
                    } finally {
                        try { image?.close() } catch (_: Exception) {}
                        codec.releaseOutputBuffer(outIdx, false)
                    }
                }
            }
        } finally {
            try { codec?.stop() } catch (_: Exception) {}
            try { codec?.release() } catch (_: Exception) {}
            try { extractor.release() } catch (_: Exception) {}
            try { fis?.close() } catch (_: Exception) {}
        }
    }

    private fun encodeYuvImageScaledJpeg(
        image: Image,
        dstW: Int,
        dstH: Int,
        baos: ByteArrayOutputStream,
    ): ByteArray {
        val crop = image.cropRect
        val srcW = crop.width().coerceAtLeast(2)
        val srcH = crop.height().coerceAtLeast(2)

        val yPlane = image.planes[0]
        val uPlane = image.planes[1]
        val vPlane = image.planes[2]
        val yBuf: ByteBuffer = yPlane.buffer
        val uBuf: ByteBuffer = uPlane.buffer
        val vBuf: ByteBuffer = vPlane.buffer

        val yRowStride = yPlane.rowStride
        val yPixStride = yPlane.pixelStride
        val uRowStride = uPlane.rowStride
        val uPixStride = uPlane.pixelStride
        val vRowStride = vPlane.rowStride
        val vPixStride = vPlane.pixelStride

        val nv21 = ByteArray(dstW * dstH * 3 / 2)
        val uvBase = dstW * dstH

        val xMap = IntArray(dstW)
        for (dx in 0 until dstW) {
            xMap[dx] = crop.left + (dx * srcW) / dstW
        }

        for (dy in 0 until dstH) {
            val sy = crop.top + (dy * srcH) / dstH
            val yRowOff = sy * yRowStride
            val dstRowOff = dy * dstW
            for (dx in 0 until dstW) {
                val sx = xMap[dx]
                nv21[dstRowOff + dx] = yBuf.get(yRowOff + sx * yPixStride)
            }
        }

        val halfDstW = dstW / 2
        val halfDstH = dstH / 2
        for (dy in 0 until halfDstH) {
            val sy = crop.top + ((dy * 2) * srcH) / dstH
            val cy = sy / 2
            val uRowOff = cy * uRowStride
            val vRowOff = cy * vRowStride
            val dstUvRow = uvBase + dy * dstW
            for (dx in 0 until halfDstW) {
                val sx = xMap[dx * 2]
                val cx = sx / 2
                // NV21 stores interleaved V then U bytes
                nv21[dstUvRow + dx * 2] = vBuf.get(vRowOff + cx * vPixStride)
                nv21[dstUvRow + dx * 2 + 1] = uBuf.get(uRowOff + cx * uPixStride)
            }
        }

        baos.reset()
        YuvImage(nv21, ImageFormat.NV21, dstW, dstH, null)
            .compressToJpeg(Rect(0, 0, dstW, dstH), JPEG_QUALITY, baos)
        return baos.toByteArray()
    }

    private fun decodeViaMetadataRetriever(
        inputFile: File,
        outFrames: MutableList<ByteArray>,
        onProgress: (Int, String) -> Unit,
    ): Triple<Int, Int, Int> {
        val mmr = MediaMetadataRetriever()
        var fis: FileInputStream? = null
        try {
            fis = FileInputStream(inputFile)
            mmr.setDataSource(fis.fd)
            val durMs = (mmr.extractMetadata(MediaMetadataRetriever.METADATA_KEY_DURATION)?.toLongOrNull() ?: 8000L)
                .coerceIn(1000L, 15_000L)
            val rawW = mmr.extractMetadata(MediaMetadataRetriever.METADATA_KEY_VIDEO_WIDTH)?.toIntOrNull() ?: 1280
            val rawH = mmr.extractMetadata(MediaMetadataRetriever.METADATA_KEY_VIDEO_HEIGHT)?.toIntOrNull() ?: 720
            val (outW, outH) = computeScaledSize(rawW, rawH)
            val fallbackFps = 12
            val maxFallbackFrames = 180
            val stepUs = 1_000_000L / fallbackFps
            val count = ((durMs * 1000L) / stepUs).toInt().coerceIn(1, maxFallbackFrames)
            val baos = ByteArrayOutputStream(65536)

            for (i in 0 until count) {
                val timeUs = i * stepUs
                val bmp = if (Build.VERSION.SDK_INT >= 27) {
                    mmr.getScaledFrameAtTime(timeUs, MediaMetadataRetriever.OPTION_CLOSEST, outW, outH)
                } else {
                    mmr.getFrameAtTime(timeUs, MediaMetadataRetriever.OPTION_CLOSEST)?.let { raw ->
                        if (raw.width == outW && raw.height == outH) raw
                        else Bitmap.createScaledBitmap(raw, outW, outH, true).also { raw.recycle() }
                    }
                }
                if (bmp != null) {
                    val finalBmp = if (bmp.width == outW && bmp.height == outH) {
                        bmp
                    } else {
                        Bitmap.createScaledBitmap(bmp, outW, outH, true).also { bmp.recycle() }
                    }
                    try {
                        baos.reset()
                        finalBmp.compress(Bitmap.CompressFormat.JPEG, JPEG_QUALITY, baos)
                        outFrames.add(baos.toByteArray())
                    } finally {
                        finalBmp.recycle()
                    }
                }
                if (i == 0 || i % 10 == 0 || i + 1 == count) {
                    val pct = (15 + ((i + 1).toDouble() / count.toDouble()) * 75.0).roundToInt().coerceIn(15, 90)
                    onProgress(pct, "正在兼容提取视频帧 (${i + 1}/$count)…")
                }
            }
            return Triple(outW, outH, fallbackFps * 1000)
        } finally {
            try { mmr.release() } catch (_: Exception) {}
            try { fis?.close() } catch (_: Exception) {}
        }
    }

    private fun tryExtractAudioTrack(inputFile: File, outputM4a: File): Boolean {
        val extractor = MediaExtractor()
        var muxer: MediaMuxer? = null
        var fis: FileInputStream? = null
        var started = false
        try {
            fis = FileInputStream(inputFile)
            extractor.setDataSource(fis.fd)
            var audioTrack = -1
            var audioFormat: MediaFormat? = null
            for (i in 0 until extractor.trackCount) {
                val fmt = extractor.getTrackFormat(i)
                val mime = fmt.getString(MediaFormat.KEY_MIME) ?: ""
                if (mime.startsWith("audio/mp4a") || mime.startsWith("audio/aac") || mime.startsWith("audio/mpeg")) {
                    audioTrack = i
                    audioFormat = fmt
                    break
                }
            }
            if (audioTrack < 0 || audioFormat == null) return false

            extractor.selectTrack(audioTrack)
            muxer = MediaMuxer(outputM4a.absolutePath, MediaMuxer.OutputFormat.MUXER_OUTPUT_MPEG_4)
            val muxTrack = muxer.addTrack(audioFormat)
            muxer.start()
            started = true

            val buf = ByteBuffer.allocateDirect(256 * 1024)
            val info = MediaCodec.BufferInfo()
            while (true) {
                buf.clear()
                val size = extractor.readSampleData(buf, 0)
                if (size < 0) break
                val pts = extractor.sampleTime
                if (pts > MAX_DURATION_US) break
                info.offset = 0
                info.size = size
                info.presentationTimeUs = pts
                info.flags = extractor.sampleFlags
                muxer.writeSampleData(muxTrack, buf, info)
                extractor.advance()
            }
            return outputM4a.isFile && outputM4a.length() > 128L
        } catch (_: Throwable) {
            outputM4a.delete()
            return false
        } finally {
            if (started) {
                try { muxer?.stop() } catch (_: Exception) {}
            }
            try { muxer?.release() } catch (_: Exception) {}
            try { extractor.release() } catch (_: Exception) {}
            try { fis?.close() } catch (_: Exception) {}
        }
    }
}
