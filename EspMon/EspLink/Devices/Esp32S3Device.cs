using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EL
{
	[EspDevice("ESP32-S3", 0x09)]
	internal class Esp32S3Device : EspDevice
	{

		public override int FlashSize
		{
			get
			{
				var fid = FLASH_ID;
				byte sizeId = (byte)(fid >> 16);
				int result;
				if (FLASH_SIZES.TryGetValue(sizeId, out result))
				{
					return result;
				}
				return -1;
			}
		}
		public override uint FLASH_ID
		{
			get
			{
				const byte SPIFLASH_RDID = 0x9F;

				return Parent.SpiFlashCommand(SPIFLASH_RDID, Array.Empty<byte>(), 24, 0, 0, 0, Parent.DefaultTimeout);
			}
		}
		public Esp32S3Device(EspLink parent) : base(parent) { }
		public override uint IROM_MAP_START { get; } = 0x42000000;
		public override uint IROM_MAP_END { get; } = 0x44000000;

		public virtual uint DROM_MAP_START { get; } = 0x3C000000;
		public virtual uint DROM_MAP_END { get; } = 0x3E000000;

		// ESP32 uses a 4 byte status reply
		public override ushort STATUS_BYTES_LENGTH { get { return Parent.IsStub ? (ushort)2 : (ushort)4; } }

		public override uint SPI_REG_BASE { get; } = 0x60002000;
		public override byte SPI_USR_OFFS { get; } = 0x18;
		public override byte SPI_USR1_OFFS { get; } = 0x1C;
		public override byte SPI_USR2_OFFS { get; } = 0x20;
		public override short SPI_MOSI_DLEN_OFFS { get; } = 0x24;
		public override short SPI_MISO_DLEN_OFFS { get; } = 0x28;

		public override byte SPI_W0_OFFS { get; } = 0x58;

		public override uint EFUSE_RD_REG_BASE { get; } = 0x60007000+0x44;

		public override uint EFUSE_BLK0_RDATA3_REG_OFFS { get => EFUSE_RD_REG_BASE + 0x00C; }
		public override uint EFUSE_BLK0_RDATA5_REG_OFFS { get => EFUSE_RD_REG_BASE + 0x014; }
		public virtual uint EFUSE_DIS_DOWNLOAD_MANUAL_ENCRYPT_REG { get => EFUSE_RD_REG_BASE + 0x18; }

		public virtual byte EFUSE_DIS_DOWNLOAD_MANUAL_ENCRYPT { get; } = 1 << 7; // EFUSE_RD_DISABLE_DL_ENCRYPT

		public virtual uint EFUSE_SPI_BOOT_CRYPT_CNT_REG { get => EFUSE_RD_REG_BASE; } // EFUSE_BLK0_WDATA0_REG

		public virtual uint EFUSE_SPI_BOOT_CRYPT_CNT_MASK { get; } = 0x7F << 20;  // EFUSE_FLASH_CRYPT_CNT
		public virtual uint EFUSE_RD_ABS_DONE_REG { get => EFUSE_RD_REG_BASE + 0x018; }

		public virtual byte EFUSE_RD_ABS_DONE_0_MASK { get; } = 1 << 4;
		public virtual byte EFUSE_RD_ABS_DONE_1_MASK { get; } = 1 << 5;

		public virtual uint EFUSE_VDD_SPI_REG { get => EFUSE_RD_REG_BASE + 0x10; }

		public virtual uint VDD_SPI_XPD { get; } = (uint)(1 << 14);  // XPD_SDIO_REG


		public virtual uint VDD_SPI_TIEH { get; } = (uint)(1 << 15);  // XPD_SDIO_TIEH

		public virtual uint VDD_SPI_FORCE { get; } = (uint)(1 << 16); // XPD_SDIO_FORCE


		public virtual uint DR_REG_SYSCON_BASE { get; } = 0x3FF66000;

		public virtual uint APB_CTL_DATE_ADDR { get => DR_REG_SYSCON_BASE + 0x7C; }

		public virtual byte APB_CTL_DATE_V { get; } = 0x1;
		public virtual byte APB_CTL_DATE_S { get; } = 31;
		public virtual uint UART_CLKDIV_REG { get; } = 0x3FF40014;

		public virtual uint XTAL_CLK_DIVIDER { get; } = 1;

		public virtual uint RTCCALICFG1 { get; } = 0x3FF5F06C;
		public virtual uint TIMERS_RTC_CALI_VALUE { get; } = 0x01FFFFFF;
		public virtual uint TIMERS_RTC_CALI_VALUE_S { get; } = 7;

		public virtual uint GPIO_STRAP_REG { get; } = 0x3FF44038;
		public virtual uint GPIO_STRAP_VDDSPI_MASK { get; } = 1 << 5;  // GPIO_STRAP_VDDSDIO

		public virtual uint RTC_CNTL_SDIO_CONF_REG { get; } = 0x3FF48074;
		public virtual uint RTC_CNTL_XPD_SDIO_REG { get; } = (uint)(1L << 31);
		public virtual uint RTC_CNTL_DREFH_SDIO_M { get; } = (uint)(3L << 29);
		public virtual uint RTC_CNTL_DREFM_SDIO_M { get; } = (uint)(3L << 27);
		public virtual uint RTC_CNTL_DREFL_SDIO_M { get; } = (uint)(3L << 25);
		public virtual uint RTC_CNTL_SDIO_FORCE { get; } = (uint)(1L << 22);
		public virtual uint RTC_CNTL_SDIO_PD_EN { get; } = (uint)(1L << 21);

		public virtual uint UARTDEV_BUF_NO_USB_OTG { get; } = 3;
		public virtual uint UARTDEV_BUF_NO_USB_JTAG_SERIAL { get; } = 4;
		
		public virtual uint UARTDEV_BUF_NO { get; } = 0x3FCEF14C;

		public virtual uint RTCCNTL_BASE_REG { get; } = 0x60008000;
		public virtual uint RTC_CNTL_SWD_CONF_REG { get { return RTCCNTL_BASE_REG + 0x00B4; } }
		public virtual uint RTC_CNTL_SWD_AUTO_FEED_EN { get; } = (uint)(1L << 31);
		public virtual uint RTC_CNTL_SWD_WPROTECT_REG { get { return RTCCNTL_BASE_REG + 0x00B8; } }
		public virtual uint RTC_CNTL_SWD_WKEY { get; } = 0x8F1D312A;

		public virtual uint RTC_CNTL_WDTCONFIG0_REG { get { return RTCCNTL_BASE_REG + 0x0098; } }
		public virtual uint RTC_CNTL_WDTWPROTECT_REG { get { return RTCCNTL_BASE_REG + 0x00B0; } }
		public virtual uint RTC_CNTL_WDT_WKEY { get; } = 0x50D83AA1;
		public override uint FLASH_WRITE_SIZE { 
			get { 
				if(Parent.IsStub)
				{
					return 0x4000;
				}
				return base.FLASH_WRITE_SIZE;
			} 
		}
		public virtual uint USB_RAM_BLOCK { get; } = 0x800;  // Max block size USB-OTG is used
		public virtual IReadOnlyDictionary<byte, int> FLASH_SIZES { get; } = new Dictionary<byte, int>() {
			{ 0x00, 1*1024 },
			{ 0x10, 2*1024 },
			{ 0x20, 4*1024 },
			{ 0x30, 8*1024 },
			{ 0x40, 16*1024 },
			{ 0x50, 32*1024 },
			{ 0x60, 64*1024 },
			{ 0x70, 128*1024 }
		};

		public override uint BOOTLOADER_FLASH_OFFSET { get; } = 0x1000;


		public virtual uint UF2_FAMILY_ID { get; } = 0x1C5F21B0;
		async Task<uint> GetUartNoAsync(CancellationToken cancellationToken, int timeout = -1)
		{
			// Read the UARTDEV_BUF_NO register to get the number of the currently used console
			var result = (await Parent.ReadRegAsync(UARTDEV_BUF_NO,cancellationToken,timeout) & 0xFF);
			return result;
		}
        async Task<bool> UsesUsbOtgAsync(CancellationToken cancellationToken, int timeout = -1) 
		{
			return await GetUartNoAsync(cancellationToken, timeout) == UARTDEV_BUF_NO_USB_OTG;
		}
		async Task<bool> UsesUsbJtagSerialAsync(CancellationToken cancellationToken, int timeout = -1)
		{
			return await GetUartNoAsync(cancellationToken, timeout) == UARTDEV_BUF_NO_USB_JTAG_SERIAL;
		}
		async Task DisableWatchdogsAsync(CancellationToken cancellationToken, int timeout = -1)
		{
			// When USB-JTAG/Serial is used, the RTC WDT and SWD watchdog are not reset
			// and can then reset the board during flashing. Disable them.
			if(await UsesUsbJtagSerialAsync(cancellationToken,timeout))
			{
				// disable RTC WDT
				await Parent.WriteRegAsync(RTC_CNTL_WDTWPROTECT_REG, RTC_CNTL_WDT_WKEY, cancellationToken, 0xFFFFFFFF, 0, 0, timeout);
				await Parent.WriteRegAsync(RTC_CNTL_WDTCONFIG0_REG, 0, cancellationToken, 0xFFFFFFFF, 0, 0, timeout);
				await Parent.WriteRegAsync(RTC_CNTL_WDTWPROTECT_REG, 0, cancellationToken, 0xFFFFFFFF, 0, 0, timeout);

				// Feed SW WDT
				await Parent.WriteRegAsync(RTC_CNTL_SWD_WPROTECT_REG, RTC_CNTL_SWD_WKEY, cancellationToken, 0xFFFFFFFF, 0, 0, timeout);
				var conf = await Parent.ReadRegAsync(RTC_CNTL_SWD_CONF_REG, cancellationToken, timeout);
				await Parent.WriteRegAsync(RTC_CNTL_SWD_CONF_REG, conf | RTC_CNTL_SWD_AUTO_FEED_EN, cancellationToken, 0xFFFFFFFF, 0, 0, timeout);
				await Parent.WriteRegAsync(RTC_CNTL_SWD_WPROTECT_REG, 0, cancellationToken, 0xFFFFFFFF, 0, 0, timeout);
			}
		}
		protected override async Task OnConnectedAsync(CancellationToken cancellationToken, int timeout)
		{
			var isOtg = await UsesUsbOtgAsync(cancellationToken, timeout);
			if(isOtg)
			{
				ESP_RAM_BLOCK = USB_RAM_BLOCK;
				
			}
			var isJtag= await UsesUsbJtagSerialAsync(cancellationToken, timeout);
			if (isJtag)
			{
				await DisableWatchdogsAsync(cancellationToken, timeout);
			}
		}

	}
	
}
