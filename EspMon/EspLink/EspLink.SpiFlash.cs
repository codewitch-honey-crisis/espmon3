using System;
using System.Threading;
using System.Threading.Tasks;
namespace EL
{
	partial class EspLink
	{
		bool _isSpiFlashAttached;
		async Task SpiSetLengthsAsync(uint mosi_bits, uint miso_bits, uint addr_bits, uint dummy_bits, CancellationToken cancellationToken, int timeout = -1)
		{
			// SPI registers, base address differs ESP32* vs 8266
			var bas = Device.SPI_REG_BASE;

			var SPI_MOSI_DLEN_REG = bas + Device.SPI_MOSI_DLEN_OFFS;

			var SPI_MISO_DLEN_REG = bas + Device.SPI_MISO_DLEN_OFFS;
			var SPI_USR1_REG = bas + Device.SPI_USR1_OFFS;
			const int SPI_USR_ADDR_LEN_SHIFT = 26;
			if (mosi_bits > 0) {
				await WriteRegAsync( (uint)SPI_MOSI_DLEN_REG, (uint)(mosi_bits - 1),cancellationToken,  0xFFFFFFFF, 0, 0, timeout);
			}
			if (miso_bits > 0) {
				await WriteRegAsync((uint)SPI_MISO_DLEN_REG, (uint)(miso_bits - 1),cancellationToken, 0xFFFFFFFF, 0, 0, timeout);
			}
			var flags = 0L;
			if (dummy_bits > 0) {
				flags |= dummy_bits - 1;
			}
			if (addr_bits > 0) {
				flags |= (addr_bits - 1L) << SPI_USR_ADDR_LEN_SHIFT;
			}

			if (flags != 0) {
				await WriteRegAsync((uint)SPI_USR1_REG, (uint)(flags), cancellationToken, 0xFFFFFFFF, 0, 0, timeout);
			}
		}
		async Task SpiSetLengthsOldAsync(uint mosi_bits, uint miso_bits, uint addr_bits, uint dummy_bits, CancellationToken cancellationToken, int timeout = -1)
		{
			// SPI registers, base address differs ESP32* vs 8266
			var bas = Device.SPI_REG_BASE;
			uint SPI_USR1_REG = bas + Device.SPI_USR1_OFFS;
			uint SPI_DATA_LEN_REG = SPI_USR1_REG;
			const int SPI_MOSI_BITLEN_S = 17;
			const int SPI_MISO_BITLEN_S = 8;
			const int SPI_USR_ADDR_LEN_SHIFT = 26;
			var mosi_mask = mosi_bits== 0 ? 0 : (uint)(mosi_bits- 1);
			var miso_mask = miso_bits== 0 ? 0 : (uint)(miso_bits - 1);
			var flags = (((long)miso_mask) << SPI_MISO_BITLEN_S) | (
					((long)mosi_mask) << SPI_MOSI_BITLEN_S
				);

			if (dummy_bits > 0) {
				flags |= dummy_bits - 1;
			}
			if (addr_bits > 0) {
				flags |= ((long)(addr_bits - 1) << SPI_USR_ADDR_LEN_SHIFT);
			}
			await WriteRegAsync((uint)SPI_DATA_LEN_REG, (uint)(flags),cancellationToken,  0xFFFFFFFF, 0, 0, timeout);
		}
		async Task SpiWaitDoneAsync(CancellationToken cancellationToken, int timeout = -1)
		{
			const uint SPI_CMD_USR = (uint)(1L << 18);
			var bas = Device.SPI_REG_BASE;

			var SPI_CMD_REG = bas + 0x00;

			for (var retr=0; retr < 10; ++retr)
			{
				var ret = await ReadRegAsync(SPI_CMD_REG, cancellationToken, timeout) & SPI_CMD_USR;
				if (ret == 0)
				{
					return;
				}
			}
			throw new TimeoutException("SPI command timed out");
		}
		async Task<uint> SpiReadPadsAsync(CancellationToken cancellationToken, int timeout = -1)
		{
			// Read chip spi pad config
			var efuse_blk0_rdata5 = await ReadRegAsync(Device.EFUSE_BLK0_RDATA5_REG_OFFS,cancellationToken, timeout);
			var spi_pad_clk = efuse_blk0_rdata5 & 0x1F;
			var spi_pad_q = (efuse_blk0_rdata5 >> 5) & 0x1F;
			var spi_pad_d = (efuse_blk0_rdata5 >> 10) & 0x1F;
			var spi_pad_cs = (efuse_blk0_rdata5 >> 15) & 0x1F;
			var efuse_blk0_rdata3_reg = await ReadRegAsync(Device.EFUSE_BLK0_RDATA3_REG_OFFS,cancellationToken, timeout);
			var spi_pad_hd = (efuse_blk0_rdata3_reg >> 4) & 0x1F;
			return (spi_pad_hd << 24) | (spi_pad_cs << 18) | (spi_pad_d << 12) | (spi_pad_q << 6) | spi_pad_clk;
		}
		async Task SpiFlashAttachAsync(CancellationToken cancellationToken,uint hspi_arg=0, int timeout = -1)
		{

			if (_isSpiFlashAttached)
			{
				return;
			}
			
			//"""Send SPI attach command to enable the SPI flash pins
			//ESP8266 ROM does this when you send flash_begin, ESP32 ROM
			//has it as a SPI command.

			// last 3 bytes in ESP_SPI_ATTACH argument are reserved values
			//arg = struct.pack("<I", hspi_arg)
			
			byte[] data;
			if(!IsStub)
			{
				data = new byte[8];
				PackUInts(data, 0, new uint[] { hspi_arg, 0 });

				// ESP32 ROM loader takes additional 'is legacy' arg, which is not
				// currently supported in the stub loader or esptool.py
				// (as it's not usually needed.
			} else
			{
				data = BitConverter.GetBytes(hspi_arg);
			}

			await CheckCommandAsync("configure SPI flash pins", Device.ESP_SPI_ATTACH, data,0,cancellationToken, timeout);
			_isSpiFlashAttached = true;
		}
		internal uint SpiFlashCommand(byte cmd, byte[] data, uint read_bits = 0, uint addr = 0, uint addr_len = 0, uint dummy_len = 0, int timeout = -1)
		{
			var task = SpiFlashCommandAsync(cmd,data,CancellationToken.None,read_bits,addr,addr_len,dummy_len,timeout);
			return task.Result;
		}
		internal async Task<uint> SpiFlashCommandAsync(byte cmd, byte[] data, CancellationToken cancellationToken, uint read_bits = 0, uint addr = 0, uint addr_len = 0, uint dummy_len = 0, int timeout = -1)
		{
			if (!_isSpiFlashAttached)
			{
				if (!IsStub)
				{
					if (Device.CHIP_NAME == "ESP32")
					{
						await SpiFlashAttachAsync(cancellationToken,await SpiReadPadsAsync(cancellationToken, timeout), timeout);
					}
				}
			}
			// if the above doesn't attach, we fallback here
			await SpiFlashAttachAsync(cancellationToken, 0, timeout);
			// Run an arbitrary SPI flash command.
			// This function uses the "USR_COMMAND" functionality in the ESP SPI hardware, rather than the precanned commands supported by hardware.So the value of spiflash_command is an actual command byte, sent over the wire.

			// After writing command byte, writes 'data' to MOSI and then reads back 'read_bits' of reply on MISO. Result is a number.
			// SPI_USR register flags
			if (read_bits > 32)
			{
				throw new ArgumentOutOfRangeException(nameof(read_bits), nameof(read_bits) + " must be less than or equal to 32 bits");
			}
			if (data.Length > 64)
			{
				throw new ArgumentException(nameof(data), nameof(data) + " must be less than or equal to 64 bytes");
			}
			const uint SPI_USR_COMMAND = (uint)(1L << 31);

			const uint SPI_USR_ADDR = (uint)(1L << 30);

			const uint SPI_USR_DUMMY = (uint)(1L << 29);

			const uint SPI_USR_MISO = (uint)(1L << 28);

			const uint SPI_USR_MOSI = (uint)(1L << 27);
			// SPI peripheral "command" bitmasks for SPI_CMD_REG
			const uint SPI_CMD_USR = (uint)(1L << 18);

			// shift values
			const int SPI_USR2_COMMAND_LEN_SHIFT = 28;

			// SPI registers, base address differs ESP32* vs 8266
			var bas = Device.SPI_REG_BASE;

			var SPI_CMD_REG = bas + 0x00;

			var SPI_ADDR_REG = bas + 0x04;

			var SPI_USR_REG = bas + Device.SPI_USR_OFFS;

			var SPI_USR1_REG = bas + Device.SPI_USR1_OFFS;

			var SPI_USR2_REG = bas + Device.SPI_USR2_OFFS;

			var SPI_W0_REG = bas + Device.SPI_W0_OFFS;
			var data_bits = (uint)(data.Length * 8);

			var old_spi_usr = await ReadRegAsync(SPI_USR_REG,cancellationToken, timeout);
			var old_spi_usr2 = await ReadRegAsync(SPI_USR2_REG, cancellationToken, timeout);

			long flags = SPI_USR_COMMAND;

			if (read_bits > 0)
			{
				flags |= SPI_USR_MISO;
			}

			if (data_bits > 0)
			{
				flags |= SPI_USR_MOSI;
			}

			if (addr_len > 0)
			{
				flags |= SPI_USR_ADDR;
			}

			if (dummy_len > 0)
			{
				flags |= SPI_USR_DUMMY;
			}

			// set lengths
			if (Device.SPI_MOSI_DLEN_OFFS!=-1) {
				// ESP32 uses newer facilities
				await SpiSetLengthsAsync(data_bits, read_bits, addr_len, dummy_len, cancellationToken, timeout);

			} else {
				await SpiSetLengthsOldAsync(data_bits, read_bits, addr_len, dummy_len,cancellationToken, timeout);
			}
			// end set lengths
			await WriteRegAsync(SPI_USR_REG, (uint)flags,cancellationToken, 0xFFFFFFFF, 0, 0, timeout);

			await WriteRegAsync(SPI_USR2_REG, (uint)((7L << SPI_USR2_COMMAND_LEN_SHIFT) | cmd),cancellationToken, 0xFFFFFFFF, 0, 0, timeout);
			if (addr_len > 0)
			{

				addr = unchecked((uint)(((long)addr) << (32 - (int)addr_len)));
				await WriteRegAsync(SPI_ADDR_REG, addr, cancellationToken, 0xFFFFFFFF, 0, 0, timeout);
			}
			if (data_bits == 0)
			{
				await WriteRegAsync(SPI_W0_REG, 0,cancellationToken, 0xFFFFFFFF, 0, 0, timeout);  // clear data register before we read it
			}
			else
			{
				data = PadTo(data, 4, 0x00); // pad to 32-bit multiple
				var next_reg = SPI_W0_REG;
				if (BitConverter.IsLittleEndian)
				{
					for (int i = 0; i < data.Length; i += 4)
					{
						var val = BitConverter.ToUInt32(data, i);
						
						val = SwapBytes(val);
						
						await WriteRegAsync(next_reg, val, cancellationToken, 0xFFFFFFFF, 0, 0, timeout);
						next_reg += 4;
					}
				}
				else
				{
					for (int i = 0; i < data.Length; i += 4)
					{
						var val = BitConverter.ToUInt32(data, i);
						await WriteRegAsync(next_reg, val, cancellationToken, 0xFFFFFFFF, 0, 0, timeout);
						next_reg += 4;
					}
				}
			}

			await WriteRegAsync(SPI_CMD_REG, SPI_CMD_USR, cancellationToken, 0xFFFFFFFF, 0, 0, timeout);
			await SpiWaitDoneAsync(cancellationToken, timeout);
			var status = await ReadRegAsync(SPI_W0_REG,cancellationToken, timeout);
			// restore some SPI controller registers
			await WriteRegAsync(SPI_USR_REG, old_spi_usr, cancellationToken, 0xFFFFFFFF, 0, 0, timeout);
			await WriteRegAsync(SPI_USR2_REG, old_spi_usr2, cancellationToken, 0xFFFFFFFF, 0, 0, timeout);
			if(BitConverter.IsLittleEndian)
			{
				status = SwapBytes(status);
				//status >>= (int)(32 - read_bits);
				
			}
			return status;
		}
		
	}
}
