﻿#region BSD Licence
/* Copyright (c) 2013-2018, Doxense SAS
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:
	* Redistributions of source code must retain the above copyright
	  notice, this list of conditions and the following disclaimer.
	* Redistributions in binary form must reproduce the above copyright
	  notice, this list of conditions and the following disclaimer in the
	  documentation and/or other materials provided with the distribution.
	* Neither the name of Doxense nor the
	  names of its contributors may be used to endorse or promote products
	  derived from this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL <COPYRIGHT HOLDER> BE LIABLE FOR ANY
DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */
#endregion

namespace System
{
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.Diagnostics;
	using System.Runtime.CompilerServices;
	using System.Runtime.InteropServices;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Memory;
	using JetBrains.Annotations;

	/// <summary>Represents an RFC 4122 compliant 128-bit UUID</summary>
	/// <remarks>You should use this type if you are primarily exchanging UUIDs with non-.NET platforms, that use the RFC 4122 byte ordering (big endian). The type System.Guid uses the Microsoft encoding (little endian) and is not compatible.</remarks>
	[DebuggerDisplay("[{ToString(),nq}]")]
	[ImmutableObject(true), StructLayout(LayoutKind.Explicit), Serializable]
	public readonly struct Uuid128 : IFormattable, IComparable, IEquatable<Uuid128>, IComparable<Uuid128>, IEquatable<Guid>
	{
		// This is just a wrapper struct on System.Guid that makes sure that ToByteArray() and Parse(byte[]) and new(byte[]) will parse according to RFC 4122 (http://www.ietf.org/rfc/rfc4122.txt)
		// For performance reasons, we will store the UUID as a System.GUID (Microsoft in-memory format), and swap the bytes when needed.

		// cf 4.1.2. Layeout and Byte Order

		//    The fields are encoded as 16 octets, with the sizes and order of the
		//    fields defined above, and with each field encoded with the Most
		//    Significant Byte first (known as network byte order).  Note that the
		//    field names, particularly for multiplexed fields, follow historical
		//    practice.

		//    0                   1                   2                   3
		//    0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
		//    +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
		//    |                          time_low                             |
		//    +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
		//    |       time_mid                |         time_hi_and_version   |
		//    +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
		//    |clk_seq_hi_res |  clk_seq_low  |         node (0-1)            |
		//    +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
		//    |                         node (2-5)                            |
		//    +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+

		// UUID "view"

		[FieldOffset(0)]
		private readonly uint m_timeLow;
		[FieldOffset(4)]
		private readonly ushort m_timeMid;
		[FieldOffset(6)]
		private readonly ushort m_timeHiAndVersion;
		[FieldOffset(8)]
		private readonly byte m_clkSeqHiRes;
		[FieldOffset(9)]
		private readonly byte m_clkSeqLow;
		[FieldOffset(10)]
		private readonly byte m_node0;
		[FieldOffset(11)]
		private readonly byte m_node1;
		[FieldOffset(12)]
		private readonly byte m_node2;
		[FieldOffset(13)]
		private readonly byte m_node3;
		[FieldOffset(14)]
		private readonly byte m_node4;
		[FieldOffset(15)]
		private readonly byte m_node5;

		// packed "view"

		[FieldOffset(0)]
		private readonly Guid m_packed;

		#region Constructors...

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Uuid128(Guid guid)
			: this()
		{
			m_packed = guid;
		}

		public Uuid128(string value)
			: this(new Guid(value))
		{ }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Uuid128(Slice slice)
			: this()
		{
			m_packed = Convert(slice);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Uuid128(byte[] bytes)
			: this()
		{
			m_packed = Convert(bytes.AsSlice());
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Uuid128(int a, short b, short c, byte[] d)
			: this(new Guid(a, b, c, d))
		{ }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Uuid128(int a, short b, short c, byte d, byte e, byte f, byte g, byte h, byte i, byte j, byte k)
			: this(new Guid(a, b, c, d, e, f, g, h, i, j, k))
		{ }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Uuid128(uint a, ushort b, ushort c, byte d, byte e, byte f, byte g, byte h, byte i, byte j, byte k)
			: this(new Guid(a, b, c, d, e, f, g, h, i, j, k))
		{ }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Uuid128(Uuid64 a, Uuid64 b)
			: this()
		{
			m_packed = Convert(a, b);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Uuid128(Uuid64 a, uint b, uint c)
			: this()
		{
			m_packed = Convert(a, b, c);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static explicit operator Guid(Uuid128 uuid)
		{
			return uuid.m_packed;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static explicit operator Uuid128(Guid guid)
		{
			return new Uuid128(guid);
		}

		public static readonly Uuid128 Empty = default(Uuid128);

		/// <summary>Size is 16 bytes</summary>
		public const int SizeOf = 16;

		public static Uuid128 NewUuid()
		{
			return new Uuid128(Guid.NewGuid());
		}

		public static Guid Convert(Slice input)
		{
			input.EnsureSliceIsValid();
			if (input.Count == 0) return default(Guid);
			if (input.Count != 16) throw new ArgumentException("Slice for UUID must be exactly 16 bytes long");

			unsafe
			{
				fixed (byte* buf = &input.DangerousGetPinnableReference())
				{
					return ReadUnsafe(buf);
				}
			}
		}

		public static unsafe Guid Convert(byte* buffer, int count)
		{
			if (count == 0) return default(Guid);
			if (count != 16) throw new ArgumentException("Slice for UUID must be exactly 16 bytes long");

			return ReadUnsafe(buffer);
		}

		public static Guid Convert(Uuid64 a, Uuid64 b)
		{
			unsafe
			{
				byte* buf = stackalloc byte[16];
				a.WriteToUnsafe(buf);
				b.WriteToUnsafe(buf + 8);
				return ReadUnsafe(buf);
			}
		}

		public static Guid Convert(Uuid64 a, uint b, uint c)
		{
			unsafe
			{
				byte* buf = stackalloc byte[16];
				a.WriteToUnsafe(buf);

				buf[8] = (byte) b;
				buf[9] = (byte)(b >> 8);
				buf[10] = (byte)(b >> 16);
				buf[11] = (byte)(b >> 24);

				buf[12] = (byte) c;
				buf[13] = (byte)(c >> 8);
				buf[14] = (byte)(c >> 16);
				buf[15] = (byte)(c >> 24);

				return ReadUnsafe(buf);
			}
		}

		public static Uuid128 Parse([NotNull] string input)
		{
			return new Uuid128(Guid.Parse(input));
		}

		public static Uuid128 ParseExact([NotNull] string input, string format)
		{
			return new Uuid128(Guid.ParseExact(input, format));
		}

		public static bool TryParse(string input, out Uuid128 result)
		{
			if (!Guid.TryParse(input, out Guid guid))
			{
				result = default(Uuid128);
				return false;
			}
			result = new Uuid128(guid);
			return true;
		}

		public static bool TryParseExact(string input, string format, out Uuid128 result)
		{
			if (!Guid.TryParseExact(input, format, out Guid guid))
			{
				result = default(Uuid128);
				return false;
			}
			result = new Uuid128(guid);
			return true;
		}

		#endregion

		public long Timestamp
		{
			[Pure]
			get
			{
				long ts = m_timeLow;
				ts |= ((long)m_timeMid) << 32;
				ts |= ((long)(m_timeHiAndVersion & 0x0FFF)) << 48;
				return ts;
			}
		}

		public int Version
		{
			[Pure]
			get
			{
				return m_timeHiAndVersion >> 12;
			}
		}

		public int ClockSequence
		{
			[Pure]
			get
			{
				int clk = m_clkSeqLow;
				clk |= (m_clkSeqHiRes & 0x3F) << 8;
				return clk;
			}
		}

		public long Node
		{
			[Pure]
			get
			{
				long node;
				node = ((long)m_node0) << 40;
				node |= ((long)m_node1) << 32;
				node |= ((long)m_node2) << 24;
				node |= ((long)m_node3) << 16;
				node |= ((long)m_node4) << 8;
				node |= m_node5;
				return node;
			}
		}

		#region Unsafe I/O...

		[Pure]
		public static unsafe Guid ReadUnsafe([NotNull] byte* src)
		{
			Contract.Requires(src != null);
			Guid tmp;

			if (BitConverter.IsLittleEndian)
			{
				byte* ptr = (byte*)&tmp;

				// Data1: 32 bits, must swap
				ptr[0] = src[3];
				ptr[1] = src[2];
				ptr[2] = src[1];
				ptr[3] = src[0];
				// Data2: 16 bits, must swap
				ptr[4] = src[5];
				ptr[5] = src[4];
				// Data3: 16 bits, must swap
				ptr[6] = src[7];
				ptr[7] = src[6];
				// Data4: 64 bits, no swap required
				*(long*)(ptr + 8) = *(long*)(src + 8);
			}
			else
			{
				long* ptr = (long*)&tmp;
				ptr[0] = *(long*)(src);
				ptr[1] = *(long*)(src + 8);
			}

			return tmp;
		}

		public static Guid ReadUnsafe([NotNull] byte[] buffer, int offset)
		{
			Contract.Requires(buffer != null && offset >= 0 && offset + 15 < buffer.Length);
			unsafe
			{
				fixed (byte* ptr = &buffer[offset])
				{
					return ReadUnsafe(ptr);
				}
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe void WriteUnsafe(Guid value, [NotNull] byte* ptr)
		{
			WriteUnsafe(&value, ptr);
		}

		internal static unsafe void WriteUnsafe([NotNull] Guid* value, [NotNull] byte* ptr)
		{
			Contract.Requires(value != null && ptr != null);
			if (BitConverter.IsLittleEndian)
			{
				byte* src = (byte*) value;

				// Data1: 32 bits, must swap
				ptr[0] = src[3];
				ptr[1] = src[2];
				ptr[2] = src[1];
				ptr[3] = src[0];
				// Data2: 16 bits, must swap
				ptr[4] = src[5];
				ptr[5] = src[4];
				// Data3: 16 bits, must swap
				ptr[6] = src[7];
				ptr[7] = src[6];
				// Data4: 64 bits, no swap required
				*(long*)(ptr + 8) = *(long*)(src + 8);
			}
			else
			{
				long* src = (long*) value;
				*(long*)(ptr) = src[0];
				*(long*)(ptr + 8) = src[1];
			}

		}

		public static void WriteUnsafe(Guid value, [NotNull] byte[] buffer, int offset)
		{
			Contract.Requires(buffer != null && offset >= 0 && offset + 15 < buffer.Length);
			unsafe
			{
				fixed (byte* ptr = &buffer[offset])
				{
					WriteUnsafe(value, ptr);
				}
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public unsafe void WriteToUnsafe([NotNull] byte* ptr)
		{
			WriteUnsafe(m_packed, ptr);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteToUnsafe([NotNull] byte[] buffer, int offset)
		{
			WriteUnsafe(m_packed, buffer, offset);
		}

		#endregion

		#region Decomposition...

		/// <summary>Split this 128-bit UUID into two 64-bit UUIDs</summary>
		/// <param name="high">Receives the first 8 bytes (in network order) of this UUID</param>
		/// <param name="low">Receives the last 8 bytes (in network order) of this UUID</param>
		public void Split(out Uuid64 high, out Uuid64 low)
		{
			unsafe
			{
				byte* buffer = stackalloc byte[16];
				WriteUnsafe(m_packed, buffer);
				high = new Uuid64(Uuid64.ReadUnsafe(buffer));
				low = new Uuid64(Uuid64.ReadUnsafe(buffer + 8));
			}
		}

		/// <summary>Split this 128-bit UUID into two 64-bit numbers</summary>
		/// <param name="high">Receives the first 8 bytes (in network order) of this UUID</param>
		/// <param name="low">Receives the last 8 bytes (in network order) of this UUID</param>
		public void Split(out ulong high, out ulong low)
		{
			unsafe
			{
				byte* buffer = stackalloc byte[16];
				WriteUnsafe(m_packed, buffer);
				high = Uuid64.ReadUnsafe(buffer);
				low = Uuid64.ReadUnsafe(buffer + 8);
			}
		}

		/// <summary>Split this 128-bit UUID into two 64-bit numbers</summary>
		/// <param name="high">Receives the first 8 bytes (in network order) of this UUID</param>
		/// <param name="mid">Receives the middle 4 bytes (in network order) of this UUID</param>
		/// <param name="low">Receives the last 4 bytes (in network order) of this UUID</param>
		public void Split(out ulong high, out uint mid, out uint low)
		{
			unsafe
			{
				byte* buffer = stackalloc byte[16];
				WriteUnsafe(m_packed, buffer);
				high = Uuid64.ReadUnsafe(buffer);
				var id = Uuid64.ReadUnsafe(buffer + 8);
				mid = (uint) (id >> 32);
				low = (uint) id;
			}
		}

		#endregion

		#region Conversion...

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Guid ToGuid()
		{
			return m_packed;
		}

		[Pure, NotNull]
		public byte[] ToByteArray()
		{
			// We must use Big Endian when serializing the UUID

			var res = new byte[16];
			unsafe
			{
				fixed (byte* ptr = res)
				fixed (Uuid128* self = &this)
				{
					WriteUnsafe((Guid*) self, ptr);
				}
			}
			return res;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Slice ToSlice()
		{
			//TODO: optimize this ?
			return new Slice(ToByteArray(), 0, 16);
		}

		public override string ToString()
		{
			return m_packed.ToString("D", null);
		}

		public string ToString(string format)
		{
			return m_packed.ToString(format);
		}

		public string ToString(string format, IFormatProvider provider)
		{
			return m_packed.ToString(format, provider);
		}

		/// <summary>Increment the value of this UUID</summary>
		/// <param name="value">Positive value</param>
		/// <returns>Incremented UUID</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Uuid128 Increment([Positive] int value)
		{
			Contract.Requires(value >= 0);
			return Increment(checked((ulong)value));
		}

		/// <summary>Increment the value of this UUID</summary>
		/// <param name="value">Positive value</param>
		/// <returns>Incremented UUID</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Uuid128 Increment([Positive] long value)
		{
			Contract.Requires(value >= 0);
			return Increment(checked((ulong)value));
		}

		/// <summary>Increment the value of this UUID</summary>
		/// <param name="value">Value to add to this UUID</param>
		/// <returns>Incremented UUID</returns>
		[Pure]
		public Uuid128 Increment(ulong value)
		{
			unsafe
			{
				fixed (Uuid128* self = &this)
				{
					// serialize GUID into High Endian format
					byte* buf = stackalloc byte[16];
					WriteUnsafe((Guid*)self, buf);

					// Add the low 64 bits (in HE)
					ulong lo = UnsafeHelpers.LoadUInt64BE(buf + 8);
					ulong sum = lo + value;
					if (sum < value)
					{ // overflow occured, we must carry to the high 64 bits (in HE)
						ulong hi = UnsafeHelpers.LoadUInt64BE(buf);
						UnsafeHelpers.StoreUInt64BE(buf, unchecked(hi + 1));
					}
					UnsafeHelpers.StoreUInt64BE(buf + 8, sum);
					// deserialize back to GUID
					return new Uuid128(ReadUnsafe(buf));
				}
			}
		}

		//TODO: Decrement

		#endregion

		#region Equality / Comparison ...

		public override bool Equals(object obj)
		{
			if (obj == null) return false;
			if (obj is Uuid128 u128) return m_packed == u128.m_packed;
			if (obj is Guid g) return m_packed == g;
			//TODO: Slice? string?
			return false;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(Uuid128 other)
		{
			return m_packed == other.m_packed;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(Guid other)
		{
			return m_packed == other;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(Uuid128 a, Uuid128 b)
		{
			return a.m_packed == b.m_packed;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(Uuid128 a, Uuid128 b)
		{
			return a.m_packed != b.m_packed;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(Uuid128 a, Guid b)
		{
			return a.m_packed == b;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(Uuid128 a, Guid b)
		{
			return a.m_packed != b;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(Guid a, Uuid128 b)
		{
			return a == b.m_packed;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(Guid a, Uuid128 b)
		{
			return a != b.m_packed;
		}

		public override int GetHashCode()
		{
			return m_packed.GetHashCode();
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(Uuid128 other)
		{
			return m_packed.CompareTo(other.m_packed);
		}

		public int CompareTo(object obj)
		{
			switch (obj)
			{
				case null: return 1;
				case Uuid128 u128: return m_packed.CompareTo(u128.m_packed);
				case Guid g: return m_packed.CompareTo(g);
			}
			return m_packed.CompareTo(obj);
		}

		#endregion

		/// <summary>Instance of this times can be used to test Uuid128 for equality and ordering</summary>
		public sealed class Comparer : IEqualityComparer<Uuid128>, IComparer<Uuid128>
		{

			public static readonly Comparer Default = new Comparer();

			private Comparer()
			{ }

			public bool Equals(Uuid128 x, Uuid128 y)
			{
				return x.m_packed.Equals(y.m_packed);
			}

			public int GetHashCode(Uuid128 obj)
			{
				return obj.m_packed.GetHashCode();
			}

			public int Compare(Uuid128 x, Uuid128 y)
			{
				return x.m_packed.CompareTo(y.m_packed);
			}
		}

	}

}
