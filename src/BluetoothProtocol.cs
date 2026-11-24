using System;
using System.Collections.Generic;

namespace BoogieBoardSync
{
    internal static class BluetoothProtocol
    {
        public const byte Fend = 0xC0;
        public const byte Fesc = 0xDB;
        public const byte Tfend = 0xDC;
        public const byte Tfesc = 0xDD;

        public const byte ChannelInterrupt = 0x01;
        public const byte TypeData = 0x0A;
        public const byte TypeSetReport = 0x05;
        public const byte ReportTypeFeature = 0x03;

        private static readonly ushort[] LowTable = new ushort[]
        {
            0x0000, 0x1189, 0x2312, 0x329B, 0x4624, 0x57AD, 0x6536, 0x74BF,
            0x8C48, 0x9DC1, 0xAF5A, 0xBED3, 0xCA6C, 0xDBE5, 0xE97E, 0xF8F7
        };

        private static readonly ushort[] HighTable = new ushort[]
        {
            0x0000, 0x1081, 0x2102, 0x3183, 0x4204, 0x5285, 0x6306, 0x7387,
            0x8408, 0x9489, 0xA50A, 0xB58B, 0xC60C, 0xD68D, 0xE70E, 0xF78F
        };

        public static ushort Crc8(byte[] data, int offset, int count)
        {
            int crc = 0xFFFF;
            for (int i = 0; i < count; i++)
            {
                int value = data[offset + i] ^ (crc & 0xFF);
                crc = (crc >> 8) ^ LowTable[value & 0x0F] ^ HighTable[(value & 0xF0) >> 4];
            }
            return (ushort)crc;
        }

        public static bool IsValidPacket(byte[] packet, int count)
        {
            return count >= 4 && Crc8(packet, 0, count) == 0;
        }

        public static byte[] BuildSetReportFrame(byte reportId, byte[] payload)
        {
            if (payload == null)
            {
                payload = new byte[0];
            }

            byte[] packet = new byte[5 + payload.Length];
            packet[0] = 0x00;
            packet[1] = (byte)((TypeSetReport << 4) | ReportTypeFeature);
            packet[2] = reportId;
            packet[3] = reportId;
            packet[4] = 0x00;
            Array.Copy(payload, 0, packet, 5, payload.Length);
            return FramePacket(packet);
        }

        public static byte[] FramePacket(byte[] packet)
        {
            List<byte> output = new List<byte>(packet.Length + 6);
            output.Add(Fend);
            for (int i = 0; i < packet.Length; i++)
            {
                AppendEscaped(output, packet[i]);
            }

            ushort crc = Crc8(packet, 0, packet.Length);
            output.Add((byte)(crc & 0xFF));
            output.Add((byte)((crc >> 8) & 0xFF));
            output.Add(Fend);
            return output.ToArray();
        }

        public static bool TryGetCapturePayload(byte[] packet, int count, out byte[] payload)
        {
            payload = null;
            if (count < 12)
            {
                return false;
            }
            if (packet[0] != ChannelInterrupt)
            {
                return false;
            }
            if (((packet[1] & 0xFF) >> 4) != TypeData)
            {
                return false;
            }

            int payloadLength = count - 5;
            if (payloadLength < 7)
            {
                return false;
            }

            payload = new byte[payloadLength];
            Array.Copy(packet, 3, payload, 0, payloadLength);
            return true;
        }

        private static void AppendEscaped(List<byte> output, byte value)
        {
            if (value == Fend)
            {
                output.Add(Fesc);
                output.Add(Tfend);
            }
            else if (value == Fesc)
            {
                output.Add(Fesc);
                output.Add(Tfesc);
            }
            else
            {
                output.Add(value);
            }
        }
    }

    internal sealed class BluetoothFrameParser
    {
        private readonly List<byte> frame = new List<byte>();
        private bool inFrame;
        private bool escaped;

        public int BadFrames;
        public int ControlFrames;

        public void Reset()
        {
            frame.Clear();
            inFrame = false;
            escaped = false;
        }

        public void Feed(byte value, List<byte[]> captureReports)
        {
            if (value == BluetoothProtocol.Fend)
            {
                if (frame.Count > 0)
                {
                    byte[] packet = frame.ToArray();
                    frame.Clear();
                    if (packet.Length < 4 || !BluetoothProtocol.IsValidPacket(packet, packet.Length))
                    {
                        BadFrames++;
                    }
                    else
                    {
                        byte[] payload;
                        if (BluetoothProtocol.TryGetCapturePayload(packet, packet.Length, out payload))
                        {
                            captureReports.Add(payload);
                        }
                        else
                        {
                            ControlFrames++;
                        }
                    }
                }
                inFrame = true;
                escaped = false;
                return;
            }

            if (!inFrame)
            {
                return;
            }

            if (escaped)
            {
                if (value == BluetoothProtocol.Tfend)
                {
                    value = BluetoothProtocol.Fend;
                }
                else if (value == BluetoothProtocol.Tfesc)
                {
                    value = BluetoothProtocol.Fesc;
                }
                escaped = false;
            }
            else if (value == BluetoothProtocol.Fesc)
            {
                escaped = true;
                return;
            }

            frame.Add(value);
            if (frame.Count > 2048)
            {
                frame.Clear();
                inFrame = false;
                escaped = false;
                BadFrames++;
            }
        }
    }
}