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

namespace Doxense.Runtime.Converters
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Linq.Expressions;
	using System.Reflection;
	using System.Runtime.CompilerServices;
	using System.Threading;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Serialization;
	using JetBrains.Annotations;

	/// <summary>Helper class to convert object from one type to another</summary>
	public static class TypeConverters
	{

		/// <summary>Cache used to make the JIT inline all converters from ValueType to ValueType</summary>
		private static class Cache<TIn, TOut>
		{
			[NotNull]
			public static readonly ITypeConverter<TIn, TOut> Converter = GetConverter<TIn, TOut>();
		}

		#region Identity<T>

		/// <summary>Simple converter where the source and destination types are the same</summary>
		/// <typeparam name="T">Source and Destination type</typeparam>
		private sealed class Identity<T> : ITypeConverter<T, T>
		{
			private static readonly bool IsReferenceType = typeof(T).IsClass; //TODO: nullables ?

			public static readonly ITypeConverter<T, T> Default = new Identity<T>();

			public static readonly Func<object, T> FromObject = (Func<object, T>)TypeConverters.CreateCaster(typeof(T));

			public Type Source => typeof(T);

			public Type Destination => typeof(T);

			public T Convert(T value)
			{
				return value;
			}

			public object ConvertBoxed(object value)
			{
				return FromObject(value);
			}

			public static T Cast(object value)
			{
				if (value == null && !IsReferenceType) return default(T);
				return (T)value;
			}
		}

		#endregion

		#region Anonymous<T>

		/// <summary>Simple converter that wraps a lambda function</summary>
		/// <typeparam name="TInput">Source type</typeparam>
		/// <typeparam name="TOutput">Destination type</typeparam>
		private sealed class Anonymous<TInput, TOutput> : ITypeConverter<TInput, TOutput>
		{
			[NotNull]
			private Func<TInput, TOutput> Converter { get; }

			public Anonymous([NotNull] Func<TInput, TOutput> converter)
			{
				Contract.NotNull(converter, nameof(converter));
				this.Converter = converter;
			}

			public Type Source => typeof(TInput);

			public Type Destination => typeof(TOutput);

			public TOutput Convert(TInput value)
			{
				return this.Converter(value);
			}

			public object ConvertBoxed(object value)
			{
				return this.Converter(Identity<TInput>.FromObject(value));
			}
		}

		private sealed class SubClass<TInput, TOutput> : ITypeConverter<TInput, TOutput>
		{
			public static readonly ITypeConverter<TInput, TOutput> Default = new SubClass<TInput, TOutput>();

			private SubClass()
			{
				if (!typeof(TOutput).IsAssignableFrom(typeof(TInput))) throw new InvalidOperationException($"Type {typeof(TInput).Name} is not a subclass of {typeof(TOutput).Name}");
			}

			public TOutput Convert(TInput value)
			{
				return (TOutput)(object)value;
			}

			public Type Source
			{
				get { return typeof(TInput); }
			}

			public Type Destination
			{
				get { return typeof(TOutput); }
			}

			public object ConvertBoxed(object value)
			{
				return value;
			}
		}

		#endregion

		/// <summary>Static ctor that initialize the default converters</summary>
		static TypeConverters()
		{
			RegisterDefaultConverters();
		}

		/// <summary>Map of all known converters from T to R</summary>
		/// <remarks>No locking required, because all changes will replace this instance with a new Dictionary</remarks>
		private static Dictionary<ComparisonHelper.TypePair, ITypeConverter> Converters = new Dictionary<ComparisonHelper.TypePair, ITypeConverter>(ComparisonHelper.TypePairComparer.Default);

		/// <summary>Register all the default converters</summary>
		private static void RegisterDefaultConverters()
		{
			//TODO: there is too much generic type combinations! need to refactor this ...

			RegisterUnsafe<bool, Slice>((value) => Slice.FromByte(value ? (byte) 1 : default(byte)));
			RegisterUnsafe<bool, byte[]>((value) => Slice.FromByte(value ? (byte) 1 : default(byte)).GetBytes());
			RegisterUnsafe<bool, string>((value) => value ? "true" : "false");
			RegisterUnsafe<bool, sbyte>((value) => value ? (sbyte)1 : default(sbyte));
			RegisterUnsafe<bool, byte>((value) => value ? (byte)1 : default(byte));
			RegisterUnsafe<bool, short>((value) => value ? (short)1 : default(short));
			RegisterUnsafe<bool, ushort>((value) => value ? (ushort)1 : default(ushort));
			RegisterUnsafe<bool, int>((value) => value ? 1 : default(int));
			RegisterUnsafe<bool, uint>((value) => value ? 1U : default(uint));
			RegisterUnsafe<bool, long>((value) => value ? 1L : default(long));
			RegisterUnsafe<bool, ulong>((value) => value ? 1UL : default(ulong));
			RegisterUnsafe<bool, double>((value) => value ? 1.0d : default(double));
			RegisterUnsafe<bool, float>((value) => value ? 1.0f : default(float));
			RegisterUnsafe<bool, decimal>((value) => value ? 1m : default(decimal));

			RegisterUnsafe<int, Slice>(Slice.FromInt32);
			RegisterUnsafe<int, byte[]>((value) => Slice.FromInt32(value).GetBytes());
			RegisterUnsafe<int, string>(StringConverters.ToString);
			RegisterUnsafe<int, bool>((value) => value != 0);
			RegisterUnsafe<int, sbyte>((value) => checked((sbyte)value));
			RegisterUnsafe<int, byte>((value) => checked((byte)value));
			RegisterUnsafe<int, short>((value) => checked((short)value));
			RegisterUnsafe<int, ushort>((value) => checked((ushort)value));
			RegisterUnsafe<int, uint>((value) => (uint)value);
			RegisterUnsafe<int, long>((value) => value);
			RegisterUnsafe<int, ulong>((value) => (ulong)value);
			RegisterUnsafe<int, double>((value) => value);
			RegisterUnsafe<int, float>((value) => value); // possible loss of precision
			RegisterUnsafe<int, decimal>((value) => value);

			RegisterUnsafe<uint, Slice>(Slice.FromUInt32);
			RegisterUnsafe<uint, byte[]>((value) => Slice.FromUInt32(value).GetBytes());
			RegisterUnsafe<uint, string>(StringConverters.ToString);
			RegisterUnsafe<uint, bool>((value) => value != 0);
			RegisterUnsafe<uint, sbyte>((value) => checked((sbyte)value));
			RegisterUnsafe<uint, byte>((value) => checked((byte)value));
			RegisterUnsafe<uint, short>((value) => checked((short)value));
			RegisterUnsafe<uint, ushort>((value) => checked((ushort)value));
			RegisterUnsafe<uint, int>((value) => (int)value);
			RegisterUnsafe<uint, long>((value) => value);
			RegisterUnsafe<uint, ulong>((value) => value);
			RegisterUnsafe<uint, double>((value) => value);
			RegisterUnsafe<uint, float>((value) => value); // possible loss of precision
			RegisterUnsafe<uint, decimal>((value) => value);

			RegisterUnsafe<long, Slice>(Slice.FromInt64);
			RegisterUnsafe<long, byte[]>((value) => Slice.FromInt64(value).GetBytes());
			RegisterUnsafe<long, string>(StringConverters.ToString);
			RegisterUnsafe<long, bool>((value) => value != 0);
			RegisterUnsafe<long, sbyte>((value) => checked((sbyte)value));
			RegisterUnsafe<long, byte>((value) => checked((byte)value));
			RegisterUnsafe<long, short>((value) => checked((short)value));
			RegisterUnsafe<long, ushort>((value) => checked((ushort)value));
			RegisterUnsafe<long, int>((value) => checked((int)value));
			RegisterUnsafe<long, uint>((value) => (uint)value);
			RegisterUnsafe<long, ulong>((value) => (ulong)value);
			RegisterUnsafe<long, double>((value) => value); // possible loss of precision
			RegisterUnsafe<long, float>((value) => value); // possible loss of precision
			RegisterUnsafe<long, TimeSpan>(TimeSpan.FromTicks);
			RegisterUnsafe<long, Uuid64>((value) => new Uuid64(value));
			RegisterUnsafe<long, System.Net.IPAddress>((value) => new System.Net.IPAddress(value));
			RegisterUnsafe<long, decimal>((value) => value);

			RegisterUnsafe<ulong, Slice>(Slice.FromUInt64);
			RegisterUnsafe<ulong, byte[]>((value) => Slice.FromUInt64(value).GetBytes());
			RegisterUnsafe<ulong, string>(StringConverters.ToString);
			RegisterUnsafe<ulong, bool>((value) => value != 0);
			RegisterUnsafe<ulong, sbyte>((value) => checked((sbyte)value));
			RegisterUnsafe<ulong, byte>((value) => checked((byte)value));
			RegisterUnsafe<ulong, short>((value) => checked((short)value));
			RegisterUnsafe<ulong, ushort>((value) => checked((ushort)value));
			RegisterUnsafe<ulong, int>((value) => checked((int)value));
			RegisterUnsafe<ulong, uint>((value) => checked((uint)value));
			RegisterUnsafe<ulong, long>((value) => checked((long)value));
			RegisterUnsafe<ulong, double>((value) => value); // possible loss of precision
			RegisterUnsafe<ulong, float>((value) => value); // possible loss of precision
			RegisterUnsafe<ulong, Uuid64>((value) => new Uuid64(value));
			RegisterUnsafe<ulong, TimeSpan>((value) => TimeSpan.FromTicks(checked((long) value)));
			RegisterUnsafe<ulong, decimal>((value) => value);

			RegisterUnsafe<short, Slice>(Slice.FromInt16);
			RegisterUnsafe<short, byte[]>((value) => Slice.FromInt16(value).GetBytes());
			RegisterUnsafe<short, string>((value) => StringConverters.ToString(value));
			RegisterUnsafe<short, bool>((value) => value != 0);
			RegisterUnsafe<short, sbyte>((value) => checked((sbyte)value));
			RegisterUnsafe<short, byte>((value) => checked((byte)value));
			RegisterUnsafe<short, ushort>((value) => checked((ushort)value));
			RegisterUnsafe<short, int>((value) => value);
			RegisterUnsafe<short, uint>((value) => checked((uint)value));
			RegisterUnsafe<short, long>((value) => value);
			RegisterUnsafe<short, ulong>((value) => checked ((ulong)value));
			RegisterUnsafe<short, double>((value) => value);
			RegisterUnsafe<short, float>((value) => value);
			RegisterUnsafe<short, decimal>((value) => value);

			RegisterUnsafe<ushort, Slice>(Slice.FromUInt16);
			RegisterUnsafe<ushort, byte[]>((value) => Slice.FromUInt16(value).GetBytes());
			RegisterUnsafe<ushort, string>((value) => StringConverters.ToString(value));
			RegisterUnsafe<ushort, bool>((value) => value != 0);
			RegisterUnsafe<ushort, byte>((value) => checked((byte)value));
			RegisterUnsafe<ushort, sbyte>((value) => checked((sbyte)value));
			RegisterUnsafe<ushort, short>((value) => checked((short)value));
			RegisterUnsafe<ushort, int>((value) => value);
			RegisterUnsafe<ushort, uint>((value) => value);
			RegisterUnsafe<ushort, long>((value) => value);
			RegisterUnsafe<ushort, ulong>((value) => value);
			RegisterUnsafe<ushort, double>((value) => value);
			RegisterUnsafe<ushort, float>((value) => value);
			RegisterUnsafe<ushort, decimal>((value) => value);

			RegisterUnsafe<byte, Slice>(Slice.FromByte);
			RegisterUnsafe<byte, byte[]>((value) => Slice.FromByte(value).GetBytes());
			RegisterUnsafe<byte, string>((value) => StringConverters.ToString(value));
			RegisterUnsafe<byte, bool>((value) => value != 0);
			RegisterUnsafe<byte, sbyte>((value) => checked((sbyte)value));
			RegisterUnsafe<byte, short>((value) => value);
			RegisterUnsafe<byte, ushort>((value) => value);
			RegisterUnsafe<byte, int>((value) => value);
			RegisterUnsafe<byte, uint>((value) => value);
			RegisterUnsafe<byte, long>((value) => value);
			RegisterUnsafe<byte, ulong>((value) => value);
			RegisterUnsafe<byte, double>((value) => value);
			RegisterUnsafe<byte, float>((value) => value);
			RegisterUnsafe<byte, decimal>((value) => value);

			RegisterUnsafe<sbyte, Slice>((value) => Slice.FromInt64(value));
			RegisterUnsafe<sbyte, byte[]>((value) => Slice.FromInt64(value).GetBytes());
			RegisterUnsafe<sbyte, string>((value) => value.ToString(CultureInfo.InvariantCulture));	//TODO: string table!
			RegisterUnsafe<sbyte, bool>((value) => value != 0);
			RegisterUnsafe<sbyte, byte>((value) => checked((byte)value));
			RegisterUnsafe<sbyte, short>((value) => value);
			RegisterUnsafe<sbyte, ushort>((value) => checked((ushort)value));
			RegisterUnsafe<sbyte, int>((value) => value);
			RegisterUnsafe<sbyte, uint>((value) => checked((uint)value));
			RegisterUnsafe<sbyte, long>((value) => value);
			RegisterUnsafe<sbyte, ulong>((value) => checked((ulong)value));
			RegisterUnsafe<sbyte, double>((value) => value);
			RegisterUnsafe<sbyte, float>((value) => value);
			RegisterUnsafe<sbyte, decimal>((value) => value);

			RegisterUnsafe<float, Slice>(Slice.FromSingle);
			RegisterUnsafe<float, byte[]>((value) => Slice.FromSingle(value).GetBytes());
			RegisterUnsafe<float, string>((value) => value.ToString("R", CultureInfo.InvariantCulture));
			RegisterUnsafe<float, bool>((value) => !(value == 0f || float.IsNaN(value)));
			RegisterUnsafe<float, sbyte>((value) => checked((sbyte)value));
			RegisterUnsafe<float, byte>((value) => checked((byte)value));
			RegisterUnsafe<float, short>((value) => checked((short)value));
			RegisterUnsafe<float, ushort>((value) => checked((ushort)value));
			RegisterUnsafe<float, int>((value) => checked((int)value));
			RegisterUnsafe<float, uint>((value) => (uint)value);
			RegisterUnsafe<float, long>((value) => checked((long)value));
			RegisterUnsafe<float, ulong>((value) => (ulong)value);
			RegisterUnsafe<float, double>((value) => value);
			RegisterUnsafe<float, decimal>((value) => (decimal) value); // possible loss of precision

			RegisterUnsafe<double, Slice>((value) => Slice.FromDouble(value));
			RegisterUnsafe<double, byte[]>((value) => Slice.FromDouble(value).GetBytes());
			RegisterUnsafe<double, string>((value) => value.ToString("R", CultureInfo.InvariantCulture));
			RegisterUnsafe<double, bool>((value) => !(value == 0d || double.IsNaN(value)));
			RegisterUnsafe<double, sbyte>((value) => checked((sbyte)value));
			RegisterUnsafe<double, byte>((value) => checked((byte)value));
			RegisterUnsafe<double, short>((value) => checked((short)value));
			RegisterUnsafe<double, ushort>((value) => checked((ushort)value));
			RegisterUnsafe<double, int>((value) => checked((int)value));
			RegisterUnsafe<double, uint>((value) => (uint)value);
			RegisterUnsafe<double, long>((value) => checked((long)value));
			RegisterUnsafe<double, ulong>((value) => (ulong)value);
			RegisterUnsafe<double, float>((value) => (float)value); // possible loss of precision
			RegisterUnsafe<double, decimal>((value) => (decimal) value); // possible loss of precision

			RegisterUnsafe<string, Slice>((value) => Slice.FromString(value));
			RegisterUnsafe<string, byte[]>((value) => Slice.FromString(value).GetBytes()); //REVIEW: string=>byte[] use UTF-8, but byte[]=>string uses Base64 ?
			RegisterUnsafe<string, bool>((value) => !string.IsNullOrEmpty(value));
			RegisterUnsafe<string, sbyte>((value) => string.IsNullOrEmpty(value) ? default(sbyte) : sbyte.Parse(value, CultureInfo.InvariantCulture));
			RegisterUnsafe<string, byte>((value) => string.IsNullOrEmpty(value) ? default(byte) : byte.Parse(value, CultureInfo.InvariantCulture));
			RegisterUnsafe<string, short>((value) => string.IsNullOrEmpty(value) ? default(short) : short.Parse(value, CultureInfo.InvariantCulture));
			RegisterUnsafe<string, ushort>((value) => string.IsNullOrEmpty(value) ? default(ushort) : ushort.Parse(value, CultureInfo.InvariantCulture));
			RegisterUnsafe<string, int>((value) => string.IsNullOrEmpty(value) ? default(int) : int.Parse(value, CultureInfo.InvariantCulture));
			RegisterUnsafe<string, uint>((value) => string.IsNullOrEmpty(value) ? default(uint) : uint.Parse(value, CultureInfo.InvariantCulture));
			RegisterUnsafe<string, long>((value) => string.IsNullOrEmpty(value) ? default(long) : long.Parse(value, CultureInfo.InvariantCulture));
			RegisterUnsafe<string, ulong>((value) => string.IsNullOrEmpty(value) ? default(ulong) : ulong.Parse(value, CultureInfo.InvariantCulture));
			RegisterUnsafe<string, float>((value) => string.IsNullOrEmpty(value) ? default(float) : float.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture));
			RegisterUnsafe<string, double>((value) => string.IsNullOrEmpty(value) ? default(double) : double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture));
			RegisterUnsafe<string, decimal>((value) => string.IsNullOrEmpty(value) ? default(decimal) : decimal.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture));
			RegisterUnsafe<string, Guid>((value) => string.IsNullOrEmpty(value) ? default(Guid) : Guid.Parse(value));
			RegisterUnsafe<string, Uuid128>((value) => string.IsNullOrEmpty(value) ? default(Uuid128) : Uuid128.Parse(value));
			RegisterUnsafe<string, Uuid64>((value) => string.IsNullOrEmpty(value) ? default(Uuid64) : Uuid64.Parse(value));
			RegisterUnsafe<string, System.Net.IPAddress>((value) => string.IsNullOrEmpty(value) ? default(System.Net.IPAddress) : System.Net.IPAddress.Parse(value));

			RegisterUnsafe<byte[], Slice>((value) => value.AsSlice());
			RegisterUnsafe<byte[], string>((value) => value == null ? default(string) : value.Length == 0 ? string.Empty : System.Convert.ToBase64String(value)); //REVIEW: string=>byte[] use UTF-8, but byte[]=>string uses Base64 ?
			RegisterUnsafe<byte[], bool>((value) => value != null && value.Length > 0);
			RegisterUnsafe<byte[], sbyte>((value) => value == null ? default(sbyte) : value.AsSlice().ToSByte());
			RegisterUnsafe<byte[], byte>((value) => value == null ? default(byte) : value.AsSlice().ToByte());
			RegisterUnsafe<byte[], short>((value) => value == null ? default(short) : value.AsSlice().ToInt16());
			RegisterUnsafe<byte[], ushort>((value) => value == null ? default(ushort) : value.AsSlice().ToUInt16());
			RegisterUnsafe<byte[], int>((value) => value == null ? 0 : value.AsSlice().ToInt32());
			RegisterUnsafe<byte[], uint>((value) => value == null ? 0U : value.AsSlice().ToUInt32());
			RegisterUnsafe<byte[], long>((value) => value == null ? 0L : value.AsSlice().ToInt64());
			RegisterUnsafe<byte[], ulong>((value) => value == null ? 0UL : value.AsSlice().ToUInt64());
			RegisterUnsafe<byte[], Guid>((value) => value == null || value.Length == 0 ? default(Guid) : new Uuid128(value).ToGuid());
			RegisterUnsafe<byte[], Uuid128>((value) => value == null || value.Length == 0 ? default(Uuid128) : new Uuid128(value));
			RegisterUnsafe<byte[], Uuid64>((value) => value != null ? Uuid64.Read(value) : default(Uuid64));
			RegisterUnsafe<byte[], TimeSpan>((value) => value == null ? TimeSpan.Zero : TimeSpan.FromTicks(value.AsSlice().ToInt64()));
			RegisterUnsafe<byte[], System.Net.IPAddress>((value) => value == null || value.Length == 0 ? default(System.Net.IPAddress) : new System.Net.IPAddress(value));

			RegisterUnsafe<Guid, Slice>((value) => Slice.FromGuid(value));
			RegisterUnsafe<Guid, byte[]>((value) => Slice.FromGuid(value).GetBytes());
			RegisterUnsafe<Guid, string>((value) => value.ToString("D", null));
			RegisterUnsafe<Guid, Uuid128>((value) => new Uuid128(value));
			RegisterUnsafe<Guid, bool>((value) => value != Guid.Empty);
			RegisterUnsafe<Guid, System.Net.IPAddress>((value) => new System.Net.IPAddress(new Uuid128(value).ToByteArray())); //REVIEW: custom converter for Guid=>IPv6?

			RegisterUnsafe<Uuid128, Slice>((value) => value.ToSlice());
			RegisterUnsafe<Uuid128, byte[]>((value) => value.ToByteArray());
			RegisterUnsafe<Uuid128, string>((value) => value.ToString("D", null));
			RegisterUnsafe<Uuid128, Guid>((value) => value.ToGuid());
			RegisterUnsafe<Uuid128, bool>((value) => value != Uuid128.Empty);
			RegisterUnsafe<Uuid128, System.Net.IPAddress>((value) => new System.Net.IPAddress(value.ToByteArray())); //REVIEW: custom converter for Guid=>IPv6?

			RegisterUnsafe<Uuid64, Slice>((value) => value.ToSlice());
			RegisterUnsafe<Uuid64, byte[]>((value) => value.ToByteArray());
			RegisterUnsafe<Uuid64, string>((value) => value.ToString("D", null));
			RegisterUnsafe<Uuid64, long>((value) => value.ToInt64());
			RegisterUnsafe<Uuid64, ulong>((value) => value.ToUInt64());
			RegisterUnsafe<Uuid64, bool>((value) => value.ToInt64() != 0L);

			RegisterUnsafe<TimeSpan, Slice>((value) => Slice.FromInt64(value.Ticks));
			RegisterUnsafe<TimeSpan, byte[]>((value) => Slice.FromInt64(value.Ticks).GetBytes());
			RegisterUnsafe<TimeSpan, long>((value) => value.Ticks);
			RegisterUnsafe<TimeSpan, ulong>((value) => checked((ulong)value.Ticks));
			RegisterUnsafe<TimeSpan, double>((value) => value.TotalSeconds);
			RegisterUnsafe<TimeSpan, bool>((value) => value == TimeSpan.Zero);

			RegisterUnsafe<System.Net.IPAddress, Slice>((value) => (value?.GetAddressBytes()).AsSlice());
			RegisterUnsafe<System.Net.IPAddress, byte[]>((value) => value?.GetAddressBytes());
			RegisterUnsafe<System.Net.IPAddress, string>((value) => value?.ToString());
#pragma warning disable 618
			RegisterUnsafe<System.Net.IPAddress, int>((value) => (int) (value?.Address ?? 0));
#pragma warning restore 618

			RegisterUnsafe<Slice, byte[]>((value) => value.GetBytes());
			RegisterUnsafe<Slice, string>((value) => value.ToUnicode());
			RegisterUnsafe<Slice, bool>((value) => value.ToBool());
			RegisterUnsafe<Slice, sbyte>((value) => value.ToSByte());
			RegisterUnsafe<Slice, byte>((value) => value.ToByte());
			RegisterUnsafe<Slice, short>((value) => value.ToInt16());
			RegisterUnsafe<Slice, ushort>((value) => value.ToUInt16());
			RegisterUnsafe<Slice, int>((value) => value.ToInt32());
			RegisterUnsafe<Slice, uint>((value) => value.ToUInt32());
			RegisterUnsafe<Slice, long>((value) => value.ToInt64());
			RegisterUnsafe<Slice, ulong>((value) => value.ToUInt64());
			RegisterUnsafe<Slice, float>((value) => value.ToSingle());
			RegisterUnsafe<Slice, double>((value) => value.ToDouble());
			RegisterUnsafe<Slice, decimal>((value) => value.ToDecimal());
			RegisterUnsafe<Slice, Guid>((value) => value.ToGuid());
			RegisterUnsafe<Slice, Uuid128>((value) => value.ToUuid128());
			RegisterUnsafe<Slice, Uuid64>((value) => value.ToUuid64());
			RegisterUnsafe<Slice, TimeSpan>((value) => TimeSpan.FromTicks(value.ToInt64()));
			RegisterUnsafe<Slice, System.Net.IPAddress>((value) => !value.IsNullOrEmpty ? new System.Net.IPAddress(value.GetBytesOrEmpty()) : null);
		}

		/// <summary>Helper method to throw an exception when we don't know how to convert from <paramref name="source"/> to <paramref name="destination"/></summary>
		/// <param name="source">Type of the source object</param>
		/// <param name="destination">Target type of the conversion</param>
		[Pure, NotNull, MethodImpl(MethodImplOptions.NoInlining)]
		private static Exception FailCannotConvert(Type source, Type destination)
		{
			// prettyprint nullable type names to have something more usefull than "Nullable`1"
			//TODO: extend this to all generic types ?
			var nt = Nullable.GetUnderlyingType(source);
			string sourceName = nt == null ? source.Name : ("Nullable<" + nt.Name + ">");
			nt = Nullable.GetUnderlyingType(destination);
			string destinationName = nt == null ? destination.Name : ("Nullable<" + nt.Name + ">");

			return new InvalidOperationException($"Cannot convert values of type {sourceName} into {destinationName}");
		}

		/// <summary>Create a new delegate that cast a boxed valued of type T (object) into a T</summary>
		/// <returns>Delegate that is of type Func&lt;object, <param name="type"/>&gt;</returns>
		[NotNull]
		private static Delegate CreateCaster([NotNull] Type type)
		{
			var prm = Expression.Parameter(typeof(object), "value");
			//TODO: valuetype vs ref type ?
			var body = Expression.Convert(prm, type);
			var lambda = Expression.Lambda(body, true, prm);
			return lambda.Compile();
		}

		/// <summary>Helper method that wraps a lambda function into a converter</summary>
		/// <typeparam name="TInput">Source type</typeparam>
		/// <typeparam name="TOutput">Destination type</typeparam>
		/// <param name="converter">Lambda that converts a value of type <typeparamref name="TInput"/> into a value of type <typeparamref name="TOutput"/></param>
		/// <returns>Converters that wraps the lambda</returns>
		[NotNull]
		public static ITypeConverter<TInput, TOutput> Create<TInput, TOutput>([NotNull] Func<TInput, TOutput> converter)
		{
			Contract.NotNull(converter, nameof(converter));
			return new Anonymous<TInput, TOutput>(converter);
		}

		/// <summary>Add a new known converter (without locking)</summary>
		/// <typeparam name="TInput">Source type</typeparam>
		/// <typeparam name="TOutput">Destination type</typeparam>
		/// <param name="converter">Lambda that converts a value of type <typeparamref name="TInput"/> into a value of type <typeparamref name="TOutput"/></param>
		internal static void RegisterUnsafe<TInput, TOutput>([NotNull] Func<TInput, TOutput> converter)
		{
			Contract.Requires(converter != null);
			Converters[new ComparisonHelper.TypePair(typeof(TInput), typeof(TOutput))] = new Anonymous<TInput, TOutput>(converter);
		}

		/// <summary>Registers a new type converter</summary>
		/// <typeparam name="TInput">Source type</typeparam>
		/// <typeparam name="TOutput">Destination type</typeparam>
		/// <param name="converter">Lambda that converts a value of type <typeparamref name="TInput"/> into a value of type <typeparamref name="TOutput"/></param>
		public static void Register<TInput, TOutput>([NotNull] Func<TInput, TOutput> converter)
		{
			Contract.Requires(converter != null);
			Register<TInput, TOutput>(new Anonymous<TInput, TOutput>(converter));
		}

		/// <summary>Registers a new type converter</summary>
		/// <typeparam name="TInput">Source type</typeparam>
		/// <typeparam name="TOutput">Destination type</typeparam>
		/// <param name="converter">Instance that can convert values of type <typeparamref name="TInput"/> into a values of type <typeparamref name="TOutput"/></param>
		public static void Register<TInput, TOutput>([NotNull] ITypeConverter<TInput, TOutput> converter)
		{
			Contract.NotNull(converter, nameof(converter));
			while (true)
			{
				var previous = Converters;
				var dic = new Dictionary<ComparisonHelper.TypePair, ITypeConverter>(previous, previous.Comparer)
				{
					[new ComparisonHelper.TypePair(typeof(TInput), typeof(TOutput))] = converter
				};
				if (Interlocked.CompareExchange(ref Converters, dic, previous) == previous)
				{
					break;
				}
			}
		}

		/// <summary>Returns a converter that converts <typeparamref name="TInput"/>s into <typeparamref name="TOutput"/>s</summary>
		/// <typeparam name="TInput">Source type</typeparam>
		/// <typeparam name="TOutput">Destination type</typeparam>
		/// <returns>Valid convertir for this types, or an exception if there are no known convertions</returns>
		/// <exception cref="System.InvalidOperationException">No valid converter for these types was found</exception>
		[NotNull]
		public static ITypeConverter<TInput, TOutput> GetConverter<TInput, TOutput>()
		{
			if (typeof(TInput) == typeof(TOutput))
			{ // R == T : identity function
				return (ITypeConverter<TInput, TOutput>) Identity<TInput>.Default;
			}

			// Try to get from the known converters
			if (!Converters.TryGetValue(new ComparisonHelper.TypePair(typeof(TInput), typeof(TOutput)), out ITypeConverter converter))
			{
				if (typeof(TOutput).IsAssignableFrom(typeof(TInput)))
				{ // T is a subclass of R, so it should work fine
					return SubClass<TInput, TOutput>.Default;
				}

				//TODO: ..?
				throw FailCannotConvert(typeof(TInput), typeof(TOutput));
			}

			return (ITypeConverter<TInput, TOutput>) converter;
		}

		/// <summary>Wrap a Tye Converter into a corresponding Func&lt;....&gt;</summary>
		/// <typeparam name="TInput">Source type</typeparam>
		/// <typeparam name="TOutput">Destination type</typeparam>
		/// <param name="converter">Instance that can convert from <typeparamref name="TInput"/> to <typeparamref name="TOutput"/></param>
		/// <returns>Lambda function that, when called, invokes <paramref name="converter"/></returns>
		[Pure, NotNull]
		public static Func<TInput, TOutput> AsFunc<TInput, TOutput>([NotNull] this ITypeConverter<TInput, TOutput> converter)
		{
			return converter.Convert;
		}

		/// <summary>Convert a value of type <typeparamref name="TInput"/> into type <typeparamref name="TOutput"/></summary>
		/// <typeparam name="TInput">Source type</typeparam>
		/// <typeparam name="TOutput">Destination type</typeparam>
		/// <param name="value">Value to convert</param>
		/// <returns>Converted value</returns>
		[Pure, ContractAnnotation("null=>null")]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static TOutput Convert<TInput, TOutput>(TInput value)
		{
			//note: most of the types, T will be equal to R. We should get an optimized converter that will not box the values
			return Cache<TInput, TOutput>.Converter.Convert(value);
		}

		/// <summary>Cast a boxed value (known to be of type <typeparamref name="T"/>) into an unboxed value</summary>
		/// <typeparam name="T">Runtime type of the value</typeparam>
		/// <param name="value">Value that is known to be of type <typeparamref name="T"/>, but is boxed into an object</param>
		/// <returns>Original value casted into its runtime type</returns>
		[Pure, ContractAnnotation("null=>null")]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T Unbox<T>(object value)
		{
			return Identity<T>.FromObject(value);
		}

		/// <summary>Convert a boxed value into type <typeparamref name="T"/></summary>
		/// <typeparam name="T">Destination type</typeparam>
		/// <param name="value">Boxed value</param>
		/// <returns>Converted value, or an exception if there are no known convertions. The value null is converted into default(<typeparamref name="T"/>) by convention</returns>
		/// <exception cref="System.InvalidOperationException">No valid converter for these types was found</exception>
		[Pure, CanBeNull]
		public static T ConvertBoxed<T>(object value)
		{
			if (value == null) return default(T);
			var type = value.GetType();

			var targetType = typeof(T);

			// cast !
			if (targetType.IsAssignableFrom(type)) return (T) value;

			if (!Converters.TryGetValue(new ComparisonHelper.TypePair(type, targetType), out ITypeConverter converter))
			{
				// maybe it is a nullable type ?
				var nullableType = Nullable.GetUnderlyingType(targetType);
				if (nullableType == null) throw FailCannotConvert(type, targetType);

				// we already nullchecked value above, so we just have to convert it to the underlying type...

				// shortcut for converting a T into a Nullable<T> ...
				if (type == nullableType) return (T) value;

				// maybe we have a converter for the underlying type ?
				if (Converters.TryGetValue(new ComparisonHelper.TypePair(type, nullableType), out converter))
				{
					return (T) converter.ConvertBoxed(value);
				}

			}

			return (T) converter.ConvertBoxed(value);
		}

		[NotNull]
		private static MethodInfo GetConverterMethod(Type input, Type output)
		{
			var m = typeof(TypeConverters).GetMethod(nameof(GetConverter), BindingFlags.Static | BindingFlags.Public).MakeGenericMethod(input, output);
			Contract.Assert(m != null);
			return m;
		}

		/// <summary>Create a boxed converter from <typeparamref name="TInput"/> to <paramref name="outputType"/></summary>
		[Pure, NotNull]
		public static Func<TInput, object> CreateBoxedConverter<TInput>(Type outputType)
		{
			var converter = (ITypeConverter) GetConverterMethod(typeof(TInput), outputType).Invoke(null, Array.Empty<object>());
			return (x) => converter.ConvertBoxed(x);
		}

		/// <summary>Converts all the elements of a sequence</summary>
		/// <returns>New sequence with all the converted elements</returns>
		[Pure, NotNull]
		public static IEnumerable<TOutput> ConvertAll<TInput, TOutput>([NotNull] this ITypeConverter<TInput, TOutput> converter, [NotNull] IEnumerable<TInput> items)
		{
			Contract.NotNull(converter, nameof(converter));
			Contract.NotNull(items, nameof(items));

			foreach (var item in items)
			{
				yield return converter.Convert(item);
			}
		}

		/// <summary>Converts all the elements of a list</summary>
		/// <returns>New list with all the converted elements</returns>
		[NotNull]
		public static List<TOutput> ConvertAll<TInput, TOutput>([NotNull] this ITypeConverter<TInput, TOutput> converter, [NotNull] List<TInput> items)
		{
			Contract.NotNull(converter, nameof(converter));
			Contract.NotNull(items, nameof(items));

			return items.ConvertAll<TOutput>(converter.Convert);
		}

		/// <summary>Converts all the elements of an array</summary>
		/// <returns>New array with all the converted elements</returns>
		[NotNull]
		public static TOutput[] ConvertAll<TInput, TOutput>([NotNull] this ITypeConverter<TInput, TOutput> converter, [NotNull] TInput[] items)
		{
			Contract.NotNull(converter, nameof(converter));
			Contract.NotNull(items, nameof(items));

			var results = new TOutput[items.Length];
			for (int i = 0; i < items.Length; i++)
			{
				results[i] = converter.Convert(items[i]);
			}
			return results;
		}

		/// <returns></returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string ToString<TInput>(TInput value)
		{
			//note: raccourci pour Convert<TInput, string>(..) dont le but est d'être inliné par le JIT en release
			return Cache<TInput, string>.Converter.Convert(value);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static TOutput FromString<TOutput>(string text)
		{
			//note: raccourci pour Convert<TInput, string>(..) dont le but est d'être inliné par le JIT en release
			return Cache<string, TOutput>.Converter.Convert(text);
		}

	}

}
