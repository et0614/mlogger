using System;

using System.Threading;

using System.IO.Ports;
using XBeeLibrary.Core.Connection;

namespace MLServer
{
  public class SerialPortConnection : IConnectionInterface
  {

		private SerialPort sPort;

		/// <summary>インスタンスを初期化する</summary>
		/// <param name="portName">ポート名称</param>
		/// <param name="baudRate">ボーレート</param>
		public SerialPortConnection(string portName, int baudRate)
		{
			sPort = new SerialPort(portName, baudRate);

			Stream = new DataStream();
		}

    private void SPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
    {
			byte[] buff = new byte[sPort.BytesToRead];
			sPort.Read(buff, 0, sPort.BytesToRead);
			Stream.Write(buff, 0, buff.Length);

			for(int i=0;i<buff.Length;i++)
				Console.WriteLine(buff[i]);

			// Notify that data has been received.
			lock (this)
			{
				Monitor.Pulse(this);
			}
		}

    /// <summary>
    /// Attempts to open the connection interface.
    /// </summary>
    /// <seealso cref="Close"/>
    /// <seealso cref="IsOpen"/>
    public void Open()
		{
			sPort.DataReceived += SPort_DataReceived;
			try
			{
				sPort.Open();
			}
			catch
			{
				sPort.DataReceived -= SPort_DataReceived;
			}
		}

		/// <summary>
		/// Attempts to close the connection interface.
		/// </summary>
		/// <seealso cref="IsOpen"/>
		/// <seealso cref="Open"/>
		public void Close()
		{
			sPort.DataReceived -= SPort_DataReceived;
			sPort.Close();
		}

		/// <summary>
		/// Returns whether the connection interface is open or not.
		/// </summary>
		public bool IsOpen { get { return sPort.IsOpen; } }

		/// <summary>
		/// Returns the connection interface stream to read and write data.
		/// </summary>
		/// <seealso cref="DataStream"/>
		public DataStream Stream { get; }

		/// <summary>
		/// Writes the given data in the connection interface.
		/// </summary>
		/// <param name="data">The data to be written in the connection interface.</param>
		/// <seealso cref="WriteData(byte[], int, int)"/>
		public void WriteData(byte[] data)
		{
			sPort.Write(data, 0, data.Length);
		}

		/// <summary>
		/// Writes the given data in the connection interface.
		/// </summary>
		/// <param name="data">The data to be written in the connection interface.</param>
		/// <param name="offset">The start offset in the data to write.</param>
		/// <param name="length">The number of bytes to write.</param>
		/// <seealso cref="WriteData(byte[])"/>
		public void WriteData(byte[] data, int offset, int length) 
		{
			sPort.Write(data, offset, length);
		}

		/// <summary>
		/// Reads data from the connection interface and stores it in the provided byte array 
		/// returning the number of read bytes.
		/// </summary>
		/// <param name="data">The byte array to store the read data.</param>
		/// <returns>The number of bytes read.</returns>
		/// <seealso cref="ReadData(byte[], int, int)"/>
		public int ReadData(byte[] data) 
		{
			return sPort.Read(data, 0, data.Length);
		}

		/// <summary>
		/// Reads the given number of bytes at the given offset from the connection interface and 
		/// stores it in the provided byte array returning the number of read bytes.
		/// </summary>
		/// <param name="data">The byte array to store the read data.</param>
		/// <param name="offset">The start offset in data array at which the data is written.</param>
		/// <param name="length">Maximum number of bytes to read.</param>
		/// <returns>The number of bytes read.</returns>
		/// <seealso cref="ReadData(byte[])"/>
		public int ReadData(byte[] data, int offset, int length)
		{
			return sPort.Read(data, offset, length);
		}

		/// <summary>
		/// Returns the connection type of this XBee interface.
		/// </summary>
		/// <returns>The connection type of this XBee interface.</returns>
		/// <seealso cref="ConnectionType"/>
		public ConnectionType GetConnectionType()
		{
			return ConnectionType.SERIAL;
		}

		/// <summary>
		/// Sets the keys used for encryption and decryption.
		/// </summary>
		/// <param name="key">Encryption key.</param>
		/// <param name="txNonce">Transmission nonce.</param>
		/// <param name="rxNonce">Reception nonce.</param>
		public void SetEncryptionKeys(byte[] key, byte[] txNonce, byte[] rxNonce) { }

	}
}
