using System;
using System.Collections.Generic;

namespace EL
{
	[EspDevice("ESP32", 0x00F01D83)]
    internal class Esp32Device : EspDevice
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
        public Esp32Device(EspLink parent) : base(parent) { }
        public override uint IROM_MAP_START { get; } = 0x400D0000;
        public override uint IROM_MAP_END { get; } = 0x40400000;

        public virtual uint DROM_MAP_START { get; } = 0x3F400000;
        public virtual uint DROM_MAP_END { get; } = 0x3F800000;

        // ESP32 uses a 4 byte status reply
        public override ushort STATUS_BYTES_LENGTH { get { return Parent.IsStub ? (ushort)2 : (ushort)4; } } 

        public override uint SPI_REG_BASE { get; } = 0x3FF42000;
        public override byte SPI_USR_OFFS { get; } = 0x1C;
        public override byte SPI_USR1_OFFS { get; } = 0x20;
        public override byte SPI_USR2_OFFS { get; } = 0x24;
        public override short SPI_MOSI_DLEN_OFFS { get; } = 0x28;
        public override short SPI_MISO_DLEN_OFFS { get; } = 0x2C;

		public override byte SPI_W0_OFFS { get; } = 0x80;

		public override uint EFUSE_RD_REG_BASE { get; } = 0x3FF5A000;

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
        public virtual IReadOnlyDictionary<byte, int> FLASH_FREQUENCY { get; } = new Dictionary<byte, int>() {
            { 0x0F, 80 },
			{ 0x00, 40 },
            { 0x01, 26 },
            { 0x02, 20 }
        };
        public override uint BOOTLOADER_FLASH_OFFSET { get; } = 0x1000;

        public virtual IReadOnlyList<string> OVERRIDE_VDDSDIO_CHOICES { get; } = new string[] { "1.8V", "1.9V", "OFF" };
        public virtual IReadOnlyList<EspPartitionEntry> MEMORY_MAP { get; } = new EspPartitionEntry[] {
            new EspPartitionEntry( 0x00000000, 0x00010000, "PADDING"),
            new EspPartitionEntry( 0x3F400000, 0x3F800000, "DROM"),
            new EspPartitionEntry( 0x3F800000, 0x3FC00000, "EXTRAM_DATA"),
            new EspPartitionEntry( 0x3FF80000, 0x3FF82000, "RTC_DRAM"),
            new EspPartitionEntry( 0x3FF90000, 0x40000000, "BYTE_ACCESSIBLE"),
            new EspPartitionEntry( 0x3FFAE000, 0x40000000, "DRAM"),
            new EspPartitionEntry( 0x3FFE0000, 0x3FFFFFFC, "DIRAM_DRAM"),
            new EspPartitionEntry( 0x40000000, 0x40070000, "IROM"),
            new EspPartitionEntry( 0x40070000, 0x40078000, "CACHE_PRO"),
            new EspPartitionEntry( 0x40078000, 0x40080000, "CACHE_APP"),
            new EspPartitionEntry( 0x40080000, 0x400A0000, "IRAM"),
            new EspPartitionEntry( 0x400A0000, 0x400BFFFC, "DIRAM_IRAM"),
            new EspPartitionEntry( 0x400C0000, 0x400C2000, "RTC_IRAM"),
            new EspPartitionEntry( 0x400D0000, 0x40400000, "IROM"),
            new EspPartitionEntry( 0x50000000, 0x50002000, "RTC_DATA")
        };

        public virtual byte FLASH_ENCRYPTED_WRITE_ALIGN { get; } = 32;

        public virtual uint UF2_FAMILY_ID { get; } = 0x1C5F21B0;

    

	}
}
