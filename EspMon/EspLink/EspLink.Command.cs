using System;
using System.IO;
using System.Threading.Tasks;
using System.Threading;

namespace EL
{
	partial class EspLink
	{
		internal async Task<(uint Value, byte[] Data)> CommandAsync(CancellationToken cancellationToken, int op=-1, byte[] data=null, uint chk = 0, int timeout = -1)
		{
			if (op != -1)
			{
				if(data == null) data = Array.Empty<byte>();
				byte[] ba = new byte[8 + data.Length];
				PackOpPacket((byte)op, data, chk, ba, 0);
				await WriteFrameAsync(ba,0,ba.Length,cancellationToken, timeout);
			}
			for (var i = 0; i < 100; ++i)
			{
				var frame = ReadFrame(cancellationToken, timeout);
				if (frame.Length == 4 && frame[0] == 0x4f && frame[1] == 0x48 && frame[2] == 0x41 && frame[3] == 0x49)
				{
					// stub loaded frame. ignore.
					continue;
				}
				var ret = UnpackOpPacket(frame, 0);
				if (-1 == op || op == ret.Op)
				{
					return (ret.Chk, ret.Data);
				}
				else
				{
					if ((ret.Data.Length==1 && ret.Data[0]==0x05) || ret.Data.Length >= 2 && ret.Data[0] != 0 && ret.Data[1] == 0x05)
					{
						throw new InvalidOperationException("Invalid message received");
					}
				}
			}
			throw new IOException("Retry count exceeded while sending command");
		}
		internal async Task<uint> CommandResultAsync(CancellationToken cancellationToken, int op = -1, byte[] data = null, uint chk = 0, int timeout = -1)
		{
			if (op != -1)
			{
				if (data == null) data = Array.Empty<byte>();
				byte[] ba = new byte[8 + data.Length];
				PackOpPacket((byte)op, data, chk, ba, 0);
				await WriteFrameAsync(ba, 0, ba.Length, cancellationToken, timeout);
			}
			for (var i = 0; i < 100; ++i)
			{
				var frame = ReadFrame(cancellationToken, timeout);
				if (frame.Length < 8)
				{
					continue;
				}
				var ret = UnpackOpPacket(frame, 0);
				if (-1 == op || op == ret.Op)
				{
					if (ret.Data == null || ret.Data.Length == 0)
					{
						throw new IOException("Unable to read value");
					}
					return ret.Chk;
				}
				else
				{
					if ((ret.Data.Length == 1 && ret.Data[0] == 0x05) || ret.Data.Length >= 2 && ret.Data[0] != 0 && ret.Data[1] == 0x05)
					{
						throw new InvalidOperationException("Invalid message received");
					}
				}
			}
			throw new IOException("Retry count exceeded while sending command");
		}
		internal async Task<(uint Value, byte[] Data)> CheckCommandAsync(string op_description, int op, byte[] data, uint chk ,CancellationToken cancellationToken,  int timeout = -1)
		{
			(uint Value, byte[] Data) ret= await CommandAsync(cancellationToken, op, data, chk, timeout);
			
			if (ret.Data.Length < Device.STATUS_BYTES_LENGTH)
			{
				throw new IOException("Incomplete status received while performing " + op_description);
			}
			var statusBytes = new byte[Device.STATUS_BYTES_LENGTH];
			Array.Copy(ret.Data, ret.Data.Length - statusBytes.Length, statusBytes, 0, statusBytes.Length);
			if (statusBytes[0] != 0)
			{
				throw new IOException($"Failed to complete {op_description} operation (0x{((statusBytes.Length==1)? statusBytes[0].ToString("X") : BitConverter.ToUInt16(statusBytes,0).ToString("X"))})");
			}
			var remaining = new byte[ret.Data.Length - statusBytes.Length];
			if (remaining.Length > 0)
			{
				Array.Copy(ret.Data, 0, remaining, 0, remaining.Length);
			}
			ret.Data = remaining;
			return ret;
		}

		static void PackOpPacket(byte op, byte[] data, uint chk, byte[] destination, int index)
		{
			destination[index++] = 0x00;
			destination[index++] = op;
			var ba = BitConverter.GetBytes((ushort)data.Length);
			if (!BitConverter.IsLittleEndian)
			{
				Array.Reverse(ba);
			}
			Array.Copy(ba, 0, destination, index, 2);
			index += 2;
			ba = BitConverter.GetBytes(chk);
			if (!BitConverter.IsLittleEndian)
			{
				Array.Reverse(ba);
			}
			Array.Copy(ba, 0, destination, index, 4);
			index += 4;
			Array.Copy(data, 0, destination, index, data.Length);
		}
		static (byte Op, ushort Length, uint Chk, byte[] Data) UnpackOpPacket(byte[] data, int index)
		{
			if (data == null)
			{
				throw new ArgumentNullException(nameof(data));
			}
			else if (data.Length < 8 + index)
			{
				
				throw new ArgumentException("Data is not a valid packet", nameof(data));
			}
			var direction = data[index++];
			var op = data[index++];
			ushort len= BitConverter.ToUInt16(data, index);
			if (!BitConverter.IsLittleEndian)
			{
				len = SwapBytes(len);
			}
			index += 2;
			uint chk = BitConverter.ToUInt32(data, index);
			if (!BitConverter.IsLittleEndian)
			{
				chk = SwapBytes	(chk);
			}
			index += 4;
			var remData = new byte[len];
			Array.Copy(data, index, remData, 0, len);
			return (Op: op, Length: len, Chk: chk,Data: remData);
		}
	}
}
