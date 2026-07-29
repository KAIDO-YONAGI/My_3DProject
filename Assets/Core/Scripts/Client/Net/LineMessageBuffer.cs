using System;
using System.Text;

// LineMessageBuffer 只负责 TCP 字节流的缓存与换行分帧。
// 先拼接字节、再解码完整消息，避免 UTF-8 多字节字符被 TCP 分包截断。
internal sealed class LineMessageBuffer
{
    private const int InitialBufferSize = 1024;
    private const int MaxPendingByteCount = 65536;

    // 显式指定 UTF-8：与服务端编码端保持一致，避免依赖系统区域设置
    private readonly Encoding encoding = new UTF8Encoding(false, true);
    private byte[] pendingBytes = new byte[InitialBufferSize];
    private int pendingByteCount;

    public void Append(byte[] bytes, int count, Action<string> messageReceived)
    {
        EnsureCapacity(pendingByteCount + count);
        Buffer.BlockCopy(bytes, 0, pendingBytes, pendingByteCount, count);
        pendingByteCount += count;

        int messageStartIndex = 0;
        for (int i = 0; i < pendingByteCount; i++)
        {
            if (pendingBytes[i] != (byte)'\n')
            {
                continue;
            }

            int messageByteCount = i - messageStartIndex;
            if (messageByteCount > MaxPendingByteCount)
            {
                throw new InvalidOperationException("Received message exceeds the maximum allowed size.");
            }

            if (messageByteCount > 0)
            {
                // 会在 Update 中消费消息队列
                messageReceived(encoding.GetString(pendingBytes, messageStartIndex, messageByteCount));
            }

            messageStartIndex = i + 1;
        }

        if (messageStartIndex == 0)
        {
            if (pendingByteCount > MaxPendingByteCount)
            {
                throw new InvalidOperationException("Received message exceeds the maximum allowed size.");
            }

            return;
        }

        int remainingByteCount = pendingByteCount - messageStartIndex;
        if (remainingByteCount > 0)
        {
            Buffer.BlockCopy(pendingBytes, messageStartIndex, pendingBytes, 0, remainingByteCount);
        }

        pendingByteCount = remainingByteCount;
    }

    private void EnsureCapacity(int requiredCapacity)
    {
        if (requiredCapacity <= pendingBytes.Length) return;

        int newCapacity = pendingBytes.Length;
        while (newCapacity < requiredCapacity)
        {
            newCapacity *= 2;
        }

        Array.Resize(ref pendingBytes, newCapacity);
    }
}
