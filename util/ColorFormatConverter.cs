namespace WebVirtualDisplayClient.util;

public class ColorFormatConverter {
        public static byte[] PadI420(byte[] src, int srcWidth, int srcHeight, int dstWidth, int dstHeight) {
                int srcYSize = srcWidth * srcHeight;
                int srcChromaWidth = (srcWidth + 1) / 2;
                int srcChromaHeight = (srcHeight + 1) / 2;
                int srcUSize = srcChromaWidth * srcChromaHeight;

                int dstYSize = dstWidth * dstHeight;
                int dstChromaWidth = dstWidth / 2;
                int dstChromaHeight = dstHeight / 2;
                int dstUSize = dstChromaWidth * dstChromaHeight;

                byte[] dst = new byte[dstYSize + 2 * dstUSize];

                // fill U and V planes with neutral 128 BEFORE copying real data over the top-left
                Array.Fill(dst, (byte)128, dstYSize, 2 * dstUSize);
                // Y plane padding stays at 0 (black) — that part's fine as-is

                // Y plane
                for (int y = 0; y < srcHeight; y++)
                        Buffer.BlockCopy(src, y * srcWidth, dst, y * dstWidth, srcWidth);

                // U plane
                int srcUOffset = srcYSize;
                int dstUOffset = dstYSize;
                for (int y = 0; y < srcChromaHeight; y++)
                        Buffer.BlockCopy(src, srcUOffset + y * srcChromaWidth, dst, dstUOffset + y * dstChromaWidth, srcChromaWidth);

                // V plane
                int srcVOffset = srcYSize + srcUSize;
                int dstVOffset = dstYSize + dstUSize;
                for (int y = 0; y < srcChromaHeight; y++)
                        Buffer.BlockCopy(src, srcVOffset + y * srcChromaWidth, dst, dstVOffset + y * dstChromaWidth, srcChromaWidth);

                return dst;
        }
        public static byte[] BgraToI420(byte[] bgra, int width, int height, int stride)
        {
                int frameSize = width * height;
                int chromaSize = frameSize / 4;
                byte[] i420 = new byte[frameSize + 2 * chromaSize];

                int yPlane = 0;
                int uPlane = frameSize;
                int vPlane = frameSize + chromaSize;

                for (int y = 0; y < height; y++)
                {
                        int rowStart = y * stride;
                        for (int x = 0; x < width; x++)
                        {
                                int i = rowStart + x * 4;
                                byte b = bgra[i];
                                byte g = bgra[i + 1];
                                byte r = bgra[i + 2];

                                int yVal = (66 * r + 129 * g + 25 * b + 128) / 256 + 16;
                                i420[yPlane++] = (byte)Math.Clamp(yVal, 0, 255);

                                if ((y & 1) == 0 && (x & 1) == 0)
                                {
                                        int uVal = (-38 * r - 74 * g + 112 * b + 128) / 256 + 128;
                                        int vVal = (112 * r - 94 * g - 18 * b + 128) / 256 + 128;
                                        i420[uPlane++] = (byte)Math.Clamp(uVal, 0, 255);
                                        i420[vPlane++] = (byte)Math.Clamp(vVal, 0, 255);
                                }
                        }
                }

                return i420;
        }
}
