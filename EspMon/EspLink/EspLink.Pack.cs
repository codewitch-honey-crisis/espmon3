using System;

namespace EL
{
	// this is all byte manipulation mess
	partial class EspLink
	{
		internal static void PackUInts(byte[] data, int index, uint[] values)
		{
			if (data.Length - index < values.Length*4)
			{
				throw new ArgumentException("The array is not large enough");
			}
			for (int i = 0; i < values.Length; i++)
			{
				var v = BitConverter.GetBytes(values[i]);
				if (!BitConverter.IsLittleEndian)
				{
					Array.Reverse(v);
				}
				Array.Copy(v, 0, data, index, v.Length);
				index += v.Length;
			}
		}
		internal static uint Checksum(byte[] data,int index, int length, uint state=0xEF)
		{
			for(int i = index;i<index+length;++i)
			{
				state ^= data[i];
			}
			return state;
		}
		internal static uint SwapBytes(uint x)
		{
			// swap adjacent 16-bit blocks
			x = (x >> 16) | (x << 16);
			// swap adjacent 8-bit blocks
			return ((x & 0xFF00FF00) >> 8) | ((x & 0x00FF00FF) << 8);
		
		}
		internal static ushort SwapBytes(ushort x)
		{
			return (ushort)((ushort)((x & 0xff) << 8) | ((x >> 8) & 0xff));
		}
		static byte[] PadTo(byte[] data, int alignment, byte pad_character = 0xFF)
		{
			int pad_mod = data.Length % alignment;
			if (pad_mod != 0)
			{
				var result = new byte[data.Length + (alignment - pad_mod)];
				Array.Copy(data, 0, result, 0, data.Length);
				int end = data.Length + (alignment - pad_mod);
				for (int i = data.Length; i < end; ++i)
				{
					result[i] = pad_character;
				}
				return result;
			}

			return data;
		}
		
	}
}
