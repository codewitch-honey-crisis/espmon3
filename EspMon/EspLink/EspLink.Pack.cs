using System;

namespace EL
{
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

	}
}
