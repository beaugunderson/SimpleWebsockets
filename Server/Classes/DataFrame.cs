/*
Portions copyright 2011 Beau Gunderson - http://www.beaugunderson.com/
Portions copyright 2011 Olivine Labs, LLC. - http://www.olivinelabs.com/
*/

/*
This file is part of SimpleWebsockets.

SimpleWebsockets is free software: you can redistribute it and/or modify
it under the terms of the GNU Lesser General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

SimpleWebsockets is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU Lesser General Public License for more details.

You should have received a copy of the GNU Lesser General Public License
along with SimpleWebsockets.  If not, see <http://www.gnu.org/licenses/>.
*/

﻿using System;
﻿using System.Collections;
﻿using System.Collections.Generic;
﻿using System.Linq;
﻿using System.Text;

namespace SimpleWebsockets.Server.Classes
{
    /// <summary>
    /// Simple WebSocket Data Frame implementation. 
    /// Automatically manages adding received data to an existing frame and checking whether or not we've received the entire frame yet.
    /// See http://www.whatwg.org/specs/web-socket-protocol/ for more details on the WebSocket Protocol.
    /// </summary>
    public class DataFrame
    {
        /// <summary>
        /// The Dataframe's state
        /// </summary>
        public enum DataState
        {
            Empty = -1,
            Receiving = 0,
            Complete = 1
        }

        /// <summary>
        /// The internal byte buffer used to store received data until the entire frame comes through.
        /// </summary>
        private byte[] _rawFrame;

        private DataState _state = DataState.Empty;

        /// <summary>
        /// Gets the current length of the received frame.
        /// </summary>
        public int Length
        {
            get
            {
                return _rawFrame != null ? _rawFrame.Length : 0;
            }
        }

        /// <summary>
        /// Gets the state.
        /// </summary>
        public DataState State
        {
            get
            {
                return _state;
            }
        }

        // TODO: To extension method?
        public static string PrettyPrintBytes(IEnumerable<byte> bytes)
        {
            var list = bytes.Select(b => string.Format("{0:x2}", b)).ToList();

            return String.Join(":", list);
        }

        // TODO: To extension method?
        public static string PrettyPrintBits(BitArray bits)
        {
            var sb = new StringBuilder(bits.Length);

            int count = 0;

            foreach (bool bit in bits)
            {
                sb.Append(bit ? '1' : '0');

                count++;

                if (count > 0 &&
                    count % 8 == 0 &&
                    count != bits.Length)
                {
                    sb.Append(':');
                }
            }

            return sb.ToString();
        }

        // TODO: To extension method?
        public static string PrettyPrintBits(byte[] bytes)
        {
            var bits = new BitArray(bytes);

            return PrettyPrintBits(bits);
        }

        public bool IsFinished { get; set; }
        public bool IsMasked { get; set; }

        public bool Reserved1 { get; set; }
        public bool Reserved2 { get; set; }
        public bool Reserved3 { get; set; }

        public int Opcode { get; set; }
        public int OffsetBytes { get; set; }

        public string Payload { get; set; }

        public int PayloadLength
        {
            get
            {
                return PayloadBytes.Length;
            }
        }

        public byte[] PayloadBytes
        {
            get
            {
                return Encoding.UTF8.GetBytes(Payload);
            }

            set
            {
                Payload = Encoding.UTF8.GetString(value);
            }
        }

        public int TotalLength
        {
            get
            {
                return OffsetBytes + PayloadLength;
            }
        }

        public int TotalLengthBits
        {
            get
            {
                return TotalLength * 8;
            }
        }

        public DataFrame()
        {
            SetDefaults();
        }

        public DataFrame(byte[] bytes)
        {
            SetDefaults();

            PayloadBytes = bytes;
        }

        public DataFrame(string text)
        {
            SetDefaults();

            Payload = text;
        }

        private void SetDefaults()
        {
            IsFinished = true;

            Reserved1 = false;
            Reserved2 = false;
            Reserved3 = false;

            Opcode = 1; // Text

            IsMasked = false;

            OffsetBytes = 2;
        }

        public static DataFrame FromRawBytes(byte[] rawBytes)
        {
            var dataFrame = new DataFrame();

            dataFrame.Append(rawBytes, rawBytes.Length);

            return dataFrame;
        }

        /// <summary>
        /// Appends the specified data to the internal byte buffer.
        /// </summary>
        /// <param name="data">The data.</param>
        /// <param name="receivedByteCount"></param>
        public void Append(byte[] data, int receivedByteCount)
        {
            if (receivedByteCount <= 0)
            {
                Console.WriteLine("Received data with <= 0 bytes");

                return;
            }

            var bits = new BitArray(data);

            IsFinished = bits[7];
            Reserved1 = bits[6];
            Reserved2 = bits[5];
            Reserved3 = bits[4];

            IsMasked = bits[15];

            // Keep the last 4 bits
            Opcode = data[0] & 0xf;

            // Keep the last 7 bits
            int payloadLength = data[1] & 0x7f;

            //Console.WriteLine("IsFinished:\t\t{0}", IsFinished);
            //Console.WriteLine("Reserved 1:\t\t{0}", Reserved1);
            //Console.WriteLine("Reserved 2:\t\t{0}", Reserved2);
            //Console.WriteLine("Reserved 3:\t\t{0}", Reserved3);
            //Console.WriteLine("Opcode:\t\t\t{0}", Opcode);
            //Console.WriteLine("IsMasked:\t\t{0}", IsMasked);

            //Console.WriteLine("payloadLength:\t{0}", payloadLength);

            _state = IsFinished ? DataState.Complete : DataState.Receiving;

            OffsetBytes = 2;

            switch (payloadLength)
            {
                case 126:
                    OffsetBytes += 2;
                    break;
                case 127:
                    OffsetBytes += 8;
                    break;
            }

            var maskingKey = new byte[4];

            if (IsMasked)
            {
                Array.Copy(data, OffsetBytes, maskingKey, 0, 4);

                OffsetBytes += 4;
            }

            //Console.WriteLine("Offset Bytes:\t{0}", OffsetBytes);

            var truncatedData = new byte[payloadLength];

            Array.Copy(data, OffsetBytes, truncatedData, 0, payloadLength);

            //Console.WriteLine("Data:\t" + Encoding.UTF8.GetString(data));
            //Console.WriteLine("Data:\t\t" + PrettyPrintBits(data));
            //Console.WriteLine("Data:\t\t" + PrettyPrintBytes(data));
            //Console.WriteLine("Trunc:\t\t" + PrettyPrintBytes(truncatedData));

            if (IsMasked)
            {
                truncatedData = UnmaskData(truncatedData, maskingKey);

                //Console.WriteLine("Unmask:\t\t{0}", PrettyPrintBytes(truncatedData));
                //Console.WriteLine("Unmasked data: {0}", Encoding.UTF8.GetString(truncatedData));
            }

            PayloadBytes = truncatedData;

            AppendDataToFrame(truncatedData);
        }

        private static byte[] UnmaskData(byte[] data, IList<byte> maskingKey)
        {
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = (byte)(data[i] ^ maskingKey[i % 4]);
            }

            return data;
        }

        /// <summary>
        /// Appends the data to frame. Manages recreating the byte array and such.
        /// </summary>
        /// <param name="data">Some bytes.</param>
        private void AppendDataToFrame(byte[] data)
        {
            //Console.WriteLine("AppendDataToFrame called");

            var newFrame = new byte[Length + data.Length];

            // Copy _rawFrame to newFrame
            if (Length > 0)
            {
                Console.WriteLine("There was previous data in the frame");

                Array.Copy(_rawFrame, 0, newFrame, 0, Length);
            }

            // Copy data to newFrame (after _rawFrame)
            Array.Copy(data, 0, newFrame, Length, data.Length);

            _rawFrame = newFrame;
        }

        /// <summary>
        /// Returns a <see cref="System.String"/> that represents this Data Frame.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String"/> that represents this Data Frame.
        /// </returns>
        public override string ToString()
        {
            return _rawFrame != null ? Encoding.UTF8.GetString(_rawFrame) : String.Empty;
        }

        /// <summary>
        /// Returns a Byte Array that represents this Data Frame.
        /// </summary>
        /// <returns>
        /// A Byte Array that represents this Data Frame.
        /// </returns>
        public byte[] ToBytes()
        {
            var bits = new BitArray(TotalLengthBits);

            bits[7] = IsFinished;
            bits[6] = Reserved1;
            bits[5] = Reserved2;
            bits[4] = Reserved3;

            bits[15] = IsMasked;

            //Console.WriteLine("PayloadLength: {0}", PayloadLength);

            // TODO: Make this more intuitive (Reversal?)
            var opcodeBits = BitArrayFromInteger(Opcode);
            var payloadLengthBits = BitArrayFromInteger(PayloadLength);

            //Console.WriteLine("opcodeBits: {0}", PrettyPrintBits(opcodeBits));
            //Console.WriteLine("payloadLengthBits: {0}", PrettyPrintBits(payloadLengthBits));

            CopyBits(opcodeBits, 0, bits, 0, 4);
            CopyBits(payloadLengthBits, 0, bits, 8, 7);

            var bytes = new byte[TotalLength];

            bits.CopyTo(bytes, 0);

            Array.Copy(PayloadBytes, 0, bytes, OffsetBytes, PayloadLength);

            //Console.WriteLine("Created bits:\t" + PrettyPrintBits(bytes));
            //Console.WriteLine("Created bytes:\t" + PrettyPrintBytes(bytes));

            return bytes;
        }

        public BitArray BitArrayFromInteger(int integer)
        {
            var bytes = new[] { Convert.ToByte(integer) };

            return new BitArray(bytes);
        }

        public static void CopyBits(BitArray source, int sourceIndex, BitArray destination, int destinationIndex, int length)
        {
            for (int i = destinationIndex, j = sourceIndex; i < destinationIndex + length; i++, j++)
            {
                destination[i] = source[j];
            }
        }

        /// <summary>
        /// Resets and clears this instance.
        /// </summary>
        public void Clear()
        {
            _rawFrame = null;

            _state = DataState.Empty;
        }
    }
}