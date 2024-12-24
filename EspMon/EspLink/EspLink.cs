namespace EL
{
	/// <summary>
	/// Provides flashing capabilities for Espressif devices
	/// </summary>
	public partial class EspLink
	{
		/// <summary>
		/// Construct a new instance on the given COM port
		/// </summary>
		/// <param name="portName">The COM port name</param>
		public EspLink(string portName)
		{
			_portName = portName;
		}
		/// <summary>
		/// The default timeout in milliseconds
		/// </summary>
		public int DefaultTimeout { get; set; } = 5000;
		

	}
}
