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
import java.io.BufferedOutputStream
import java.io.ByteArrayOutputStream
import java.io.File
import java.io.FileInputStream
import java.io.FileOutputStream
import java.io.IOException
import java.nio.ByteBuffer
import kotlin.math.min
import kotlin.math.roundToInt
import kotlin.math.roundToLong

internal object AndroidLobbyVideoTranscoder {
    private const val MAX_DURATION_US = 300_000_000L
    private const val MAX_RAW_VIDEO_BYTES = 260L * 1024L * 1024L

    data class TranscodeResult(
        val width: Int,
        val height: Int,
        val fpsMilli: Int,
        val frameCount: Int,
        val durationSeconds: Double,
        val hasAudio: Boolean,
        val outputSizeBytes: Long,
    )

    private data class DecodeOutcome(
        val width: Int,
        val height: Int,
        val fpsMilli: Int,
        val frameLengths: IntArray,
        val capturedDurationUs: Long,
    )

    fun isTranscodableVideoFile(fileName: String): Boolean {
        val dot = fileName.lastIndexOf('.')
        if (dot < 0) return false
        return when (fileName.substring(dot).lowercase()) {
            ".mp4", ".mov", ".webm", ".m4v", ".mkv" -> true
            else -> false
        }
    }

    fun probeVideoDurationUs(inputFile: File): Long {
        val mmr = MediaMetadataRetriever()
        var fis: FileInputStream? = null
        try {
            fis = FileInputStream(inputFile)
            mmr.setDataSource(fis.fd)
            val durMs = mmr.extractMetadata(MediaMetadataRetriever.METADATA_KEY_DURATION)?.toLongOrNull()
            if (durMs != null && durMs > 0L) {
                return durMs * 1000L
            }
        } catch (_: Throwable) {
        } finally {
            try { mmr.release() } catch (_: Exception) {}
            try { fis?.close() } catch (_: Exception) {}
        }

        val extractor = MediaExtractor()
        var exFis: FileInputStream? = null
        try {
            exFis = FileInputStream(inputFile)
            extractor.setDataSource(exFis.fd)
            for (i in 0 until extractor.trackCount) {
                val fmt = extractor.getTrackFormat(i)
                val mime = fmt.getString(MediaFormat.KEY_MIME) ?: ""
                if (mime.startsWith("video/") && fmt.containsKey(MediaFormat.KEY_DURATION)) {
                    val durUs = fmt.getLong(MediaFormat.KEY_DURATION)
                    if (durUs > 0L) return durUs
                }
            }
        } catch (_: Throwable) {
        } finally {
            try { extractor.release() } catch (_: Exception) {}
            try { exFis?.close() } catch (_: Exception) {}
        }
        return 30_000_000L
    }

    fun transcodeVideoToXsvid(
        inputMp4: File,
        outputXsvid: File,
        onProgress: (Int, String) -> Unit,
    ): TranscodeResult {
        if (!inputMp4.isFile || inputMp4.length() <= 0L) {
            throw IOException("待转码视频文件无效")
        }
        onProgress(2, "正在探测视频时长与格式…")
        val sourceDurationUs = probeVideoDurationUs(inputMp4)
        val targetDurationUs = sourceDurationUs.coerceIn(1_000_000L, MAX_DURATION_US)

        val tempRawFrames = File(outputXsvid.parentFile, outputXsvid.name + ".frames.bin")
        val tempAudio = File(outputXsvid.parentFile, outputXsvid.name + ".audio.m4a")

        try {
            var outcome: DecodeOutcome? = try {
                decodeViaMediaCodec(inputMp4, tempRawFrames, sourceDurationUs, targetDurationUs, onProgress)
                    .takeIf { it.frameLengths.size >= 2 && it.width >= 2 && it.height >= 2 }
            } catch (_: Throwable) {
                null
            }

            if (outcome == null) {
                tempRawFrames.delete()
                onProgress(10, "正在切换兼容解码提取完整视频帧…")
                outcome = decodeViaMetadataRetriever(inputMp4, tempRawFrames, targetDurationUs, onProgress)
            }

            if (outcome.frameLengths.isEmpty() || outcome.width < 2 || outcome.height < 2) {
                throw IOException("未能从视频中解码出有效画面，请确认视频为 H.264 / H.265 MP4")
            }

            onProgress(91, "正在同步提取完整伴音轨…")
            val maxAudioPtsUs = min(MAX_DURATION_US, outcome.capturedDurationUs + 250_000L)
            val hasAudio = tryExtractAudioTrack(inputMp4, tempAudio, maxAudioPtsUs)

            onProgress(95, "正在流式封装完整动态背景（${outcome.frameLengths.size} 帧）…")
            val validAudio = if (hasAudio && tempAudio.isFile && tempAudio.length() > 0L) tempAudio else null
            XsvidWriter.writePackageFromRawStream(
                outputFile = outputXsvid,
                width = outcome.width,
                height = outcome.height,
                fpsMilli = outcome.fpsMilli,
                rawFramesFile = tempRawFrames,
                frameLengths = outcome.frameLengths,
                audioFile = validAudio,
            )

            val durationSec = outcome.frameLengths.size.toDouble() / (outcome.fpsMilli.toDouble() / 1000.0)
            onProgress(100, "转码完成")
            return TranscodeResult(
                width = outcome.width,
                height = outcome.height,
                fpsMilli = outcome.fpsMilli,
                frameCount = outcome.frameLengths.size,
                durationSeconds = durationSec,
                hasAudio = validAudio != null,
                outputSizeBytes = outputXsvid.length(),
            )
        } finally {
            tempRawFrames.delete()
            tempAudio.delete()
        }
    }

    private fun computeScaledSize(srcW: Int, srcH: Int, maxW: Int, maxH: Int): Pair<Int, Int> {
        val safeW = srcW.coerceAtLeast(2)
        val safeH = srcH.coerceAtLeast(2)
        val scale = min(1.0, min(maxW.toDouble() / safeW.toDouble(), maxH.toDouble() / safeH.toDouble()))
        val outW = ((safeW * scale).roundToInt() and 1.inv()).coerceIn(2, maxW)
        val outH = ((safeH * scale).roundToInt() and 1.inv()).coerceIn(2, maxH)
        return outW to outH
    }

    private fun decodeViaMediaCodec(
        inputFile: File,
        rawFramesOut: File,
        sourceDurationUs: Long,
        targetDurationUs: Long,
        onProgress: (Int, String) -> Unit,
    ): DecodeOutcome {
        val profile = XsvidWriter.selectAdaptiveProfile(targetDurationUs / 1_000_000.0, fallbackMode = false)
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
            val (outW, outH) = computeScaledSize(rawW, rawH, profile.maxWidth, profile.maxHeight)

            videoFormat.setInteger(MediaFormat.KEY_COLOR_FORMAT, MediaCodecInfo.CodecCapabilities.COLOR_FormatYUV420Flexible)
            codec = MediaCodec.createDecoderByType(mime)
            codec.configure(videoFormat, null, null, 0)
            codec.start()

            val info = MediaCodec.BufferInfo()
            val stepUs = (1_000_000.0 / profile.targetFps).roundToLong().coerceAtLeast(16_667L)
            var nextSamplePtsUs = 0L
            var sawInputEos = false
            var sawOutputEos = false
            var stoppedEarlyForLimit = false
            var noProgressLoops = 0
            var firstCapturedPtsUs = -1L
            var lastCapturedPtsUs = 0L
            var lastDecodedPtsUs = 0L
            var totalRawBytes = 0L
            val frameLengths = ArrayList<Int>(512)
            val baos = ByteArrayOutputStream(65536)

            BufferedOutputStream(FileOutputStream(rawFramesOut), 65536).use { rawOut ->
                while (!sawOutputEos && frameLengths.size < profile.maxFrames) {
                    if (!sawInputEos) {
                        val inIdx = codec.dequeueInputBuffer(10_000L)
                        if (inIdx >= 0) {
                            val inBuf = codec.getInputBuffer(inIdx)
                            if (inBuf != null) {
                                val sampleSize = extractor.readSampleData(inBuf, 0)
                                val samplePts = extractor.sampleTime
                                if (sampleSize < 0 || (samplePts >= 0L && samplePts > targetDurationUs + 500_000L)) {
                                    codec.queueInputBuffer(inIdx, 0, 0, 0L, MediaCodec.BUFFER_FLAG_END_OF_STREAM)
                                    sawInputEos = true
                                } else {
                                    val safePts = if (samplePts >= 0L) samplePts else 0L
                                    codec.queueInputBuffer(inIdx, 0, sampleSize, safePts, 0)
                                    extractor.advance()
                                }
                            }
                        }
                    }

                    val outIdx = codec.dequeueOutputBuffer(info, 15_000L)
                    if (outIdx >= 0) {
                        noProgressLoops = 0
                        var image: Image? = null
                        try {
                            if ((info.flags and MediaCodec.BUFFER_FLAG_END_OF_STREAM) != 0) {
                                sawOutputEos = true
                            }
                            if (info.size > 0 && info.presentationTimeUs <= targetDurationUs) {
                                if (info.presentationTimeUs > lastDecodedPtsUs) {
                                    lastDecodedPtsUs = info.presentationTimeUs
                                }
                                if (info.presentationTimeUs >= nextSamplePtsUs) {
                                    image = codec.getOutputImage(outIdx)
                                    if (image != null) {
                                        val jpeg = encodeYuvImageScaledJpeg(image, outW, outH, profile.jpegQuality, baos)
                                        if (totalRawBytes + jpeg.size.toLong() > MAX_RAW_VIDEO_BYTES) {
                                            stoppedEarlyForLimit = true
                                            break
                                        }
                                        rawOut.write(jpeg)
                                        frameLengths.add(jpeg.size)
                                        totalRawBytes += jpeg.size.toLong()
                                        if (firstCapturedPtsUs < 0L) {
                                            firstCapturedPtsUs = info.presentationTimeUs
                                        }
                                        lastCapturedPtsUs = info.presentationTimeUs
                                        nextSamplePtsUs += stepUs
                                        if (nextSamplePtsUs <= info.presentationTimeUs) {
                                            nextSamplePtsUs = info.presentationTimeUs + stepUs
                                        }
                                        val pct = (10 + (info.presentationTimeUs.toDouble() / targetDurationUs.toDouble()) * 80.0)
                                            .roundToInt()
                                            .coerceIn(10, 90)
                                        if (frameLengths.size == 1 || frameLengths.size % 12 == 0) {
                                            onProgress(pct, "正在硬件转码视频帧 (${frameLengths.size} 帧)…")
                                        }
                                    }
                                }
                            }
                        } finally {
                            try { image?.close() } catch (_: Exception) {}
                            codec.releaseOutputBuffer(outIdx, false)
                        }
                    } else if (sawInputEos) {
                        noProgressLoops++
                        if (noProgressLoops >= 400) {
                            break
                        }
                    }
                }
                rawOut.flush()
            }

            if (frameLengths.size >= profile.maxFrames && !sawOutputEos) {
                stoppedEarlyForLimit = true
            }

            val naturalSpan = if (firstCapturedPtsUs >= 0L) {
                maxOf(lastDecodedPtsUs - firstCapturedPtsUs, lastCapturedPtsUs - firstCapturedPtsUs + stepUs)
            } else {
                targetDurationUs
            }
            val reachedFullVideo = !stoppedEarlyForLimit &&
                frameLengths.size < profile.maxFrames &&
                sourceDurationUs in 500_000L..MAX_DURATION_US &&
                targetDurationUs >= sourceDurationUs
            val capturedDurationUs = if (reachedFullVideo) {
                maxOf(naturalSpan, sourceDurationUs)
            } else {
                naturalSpan.coerceAtLeast(stepUs)
            }
            val fallbackFpsMilli = (profile.targetFps * 1000.0).roundToInt()
            val actualFpsMilli = XsvidWriter.computeCalibratedFpsMilli(
                frameCount = frameLengths.size,
                durationUs = capturedDurationUs,
                fallbackFpsMilli = fallbackFpsMilli,
            )
            return DecodeOutcome(
                width = outW,
                height = outH,
                fpsMilli = actualFpsMilli,
                frameLengths = frameLengths.toIntArray(),
                capturedDurationUs = capturedDurationUs,
            )
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
        jpegQuality: Int,
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
            .compressToJpeg(Rect(0, 0, dstW, dstH), jpegQuality, baos)
        return baos.toByteArray()
    }

    private fun decodeViaMetadataRetriever(
        inputFile: File,
        rawFramesOut: File,
        targetDurationUs: Long,
        onProgress: (Int, String) -> Unit,
    ): DecodeOutcome {
        val profile = XsvidWriter.selectAdaptiveProfile(targetDurationUs / 1_000_000.0, fallbackMode = true)
        val mmr = MediaMetadataRetriever()
        var fis: FileInputStream? = null
        try {
            fis = FileInputStream(inputFile)
            mmr.setDataSource(fis.fd)
            val rawW = mmr.extractMetadata(MediaMetadataRetriever.METADATA_KEY_VIDEO_WIDTH)?.toIntOrNull() ?: 1280
            val rawH = mmr.extractMetadata(MediaMetadataRetriever.METADATA_KEY_VIDEO_HEIGHT)?.toIntOrNull() ?: 720
            val (outW, outH) = computeScaledSize(rawW, rawH, profile.maxWidth, profile.maxHeight)
            val stepUs = (1_000_000.0 / profile.targetFps).roundToLong().coerceAtLeast(25_000L)
            val count = (targetDurationUs / stepUs).toInt().coerceIn(1, profile.maxFrames)
            val baos = ByteArrayOutputStream(65536)
            val frameLengths = ArrayList<Int>(count)
            var totalRawBytes = 0L
            var lastCapturedTimeUs = 0L

            BufferedOutputStream(FileOutputStream(rawFramesOut), 65536).use { rawOut ->
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
                        val jpeg = try {
                            baos.reset()
                            finalBmp.compress(Bitmap.CompressFormat.JPEG, profile.jpegQuality, baos)
                            baos.toByteArray()
                        } finally {
                            finalBmp.recycle()
                        }
                        if (totalRawBytes + jpeg.size.toLong() > MAX_RAW_VIDEO_BYTES) {
                            break
                        }
                        rawOut.write(jpeg)
                        frameLengths.add(jpeg.size)
                        totalRawBytes += jpeg.size.toLong()
                        lastCapturedTimeUs = timeUs
                    }
                    if (i == 0 || i % 10 == 0 || i + 1 == count) {
                        val pct = (10 + ((i + 1).toDouble() / count.toDouble()) * 80.0).roundToInt().coerceIn(10, 90)
                        onProgress(pct, "正在兼容提取视频帧 (${i + 1}/$count)…")
                    }
                }
                rawOut.flush()
            }

            val capturedDurationUs = min(targetDurationUs, lastCapturedTimeUs + stepUs)
            val fallbackFpsMilli = (profile.targetFps * 1000.0).roundToInt()
            val actualFpsMilli = XsvidWriter.computeCalibratedFpsMilli(
                frameCount = frameLengths.size,
                durationUs = capturedDurationUs,
                fallbackFpsMilli = fallbackFpsMilli,
            )
            return DecodeOutcome(
                width = outW,
                height = outH,
                fpsMilli = actualFpsMilli,
                frameLengths = frameLengths.toIntArray(),
                capturedDurationUs = capturedDurationUs,
            )
        } finally {
            try { mmr.release() } catch (_: Exception) {}
            try { fis?.close() } catch (_: Exception) {}
        }
    }

    private fun tryExtractAudioTrack(inputFile: File, outputM4a: File, maxAudioPtsUs: Long): Boolean {
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
                if (pts >= 0L && pts > maxAudioPtsUs) break
                info.offset = 0
                info.size = size
                info.presentationTimeUs = if (pts >= 0L) pts else 0L
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
