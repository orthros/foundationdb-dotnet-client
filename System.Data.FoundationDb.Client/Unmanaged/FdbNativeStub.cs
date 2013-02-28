﻿#region BSD Licence
/* Copyright (c) 2013, Doxense SARL
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:
	* Redistributions of source code must retain the above copyright
	  notice, this list of conditions and the following disclaimer.
	* Redistributions in binary form must reproduce the above copyright
	  notice, this list of conditions and the following disclaimer in the
	  documentation and/or other materials provided with the distribution.
	* Neither the name of the <organization> nor the
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

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace System.Data.FoundationDb.Client
{
	internal static unsafe class FdbNativeStub
	{
		public const int FDB_API_VERSION = 21;

		private const string DLL_X86 = "fdb_c.dll";
		private const string DLL_X64 = "fdb_c.dll";

		private static readonly UnmanagedLibrary s_lib;

		#region Delegates ...

		private delegate byte* FdbGetErrorDelegate(FdbError code);

		private delegate FdbError FdbSelectApiVersionImplDelegate(int runtimeVersion, int headerVersion);

		private delegate int FdbGetMaxApiVersionDelegate();

		private delegate FdbError FdbNetworkSetOptionDelegate(FdbNetworkOption option, byte* value, int value_length);
		private delegate FdbError FdbSetupNetworkDelegate();
		private delegate FdbError FdbRunNetworkDelegate();
		private delegate FdbError FdbStopNetworkDelegate();

		private delegate void FdbFutureDestroy(/*FDBFuture*/IntPtr futureHandle);
		private delegate bool FdbFutureIsReady(FutureHandle futureHandle);
		private delegate bool FdbFutureIsError(FutureHandle futureHandle);
		private delegate FdbError FdbFutureGetErrorDelegate(FutureHandle future, byte** description);
		private delegate FdbError FdbFutureBlockUntilReady(FutureHandle futureHandle);
		private delegate FdbError FdbFutureSetCallbackDelegate(FutureHandle future, FdbFutureCallback callback, IntPtr callbackParameter);

		private delegate /*Future*/IntPtr FdbCreateClusterDelegate(byte* clusterFilePath);
		private delegate void FdbClusterDestroyDelegate(/*FDBCluster*/IntPtr cluster);
		private delegate FdbError FdbClusterSetOptionDelegate(ClusterHandle cluster, FdbClusterOption option, byte* value, int valueLength);
		private delegate FdbError FdbFutureGetClusterDelegate(FutureHandle future, out /*FDBCluster*/IntPtr cluster);

		private delegate void FdbDatabaseDestroyDelegate(/*FDBDatabase*/IntPtr database);
		private delegate /*Future*/IntPtr FdbClusterCreateDatabaseDelegate(ClusterHandle cluster, byte* dbName, int dbNameLength);
		private delegate FdbError FdbFutureGetDatabaseDelegate(/*Future*/IntPtr future, out /*FDBDatabase*/IntPtr database);

		private delegate FdbError FdbDatabaseCreateTransactionDelegate(DatabaseHandle database, out IntPtr transaction);
		private delegate void FdbTransactionSetDelegate(TransactionHandle transaction, byte* keyName, int keyNameLength, byte* value, int valueLength);
		private delegate /*Future*/IntPtr FdbTransactionCommitDelegate(TransactionHandle transaction);

		#endregion

		#region Stubs...

		private static readonly FdbGetErrorDelegate s_stub_fdbGetError;
		private static readonly FdbSelectApiVersionImplDelegate s_stub_fdbSelectApiVersionImpl;
		private static readonly FdbGetMaxApiVersionDelegate s_stub_fdbGetMaxApiVersion;

		private static readonly FdbNetworkSetOptionDelegate s_stub_fdbNetworkSetOption;
		private static readonly FdbSetupNetworkDelegate s_stub_fdbSetupNetwork;
		private static readonly FdbRunNetworkDelegate s_stud_fdbRunNetwork;
		private static readonly FdbStopNetworkDelegate s_stub_fdbStopNetwork;

		private static readonly FdbFutureDestroy s_stub_fdbFutureDestroy;
		private static readonly FdbFutureIsError s_stub_fdbFutureIsError;
		private static readonly FdbFutureIsReady s_stub_fdbFutureIsReady;
		private static readonly FdbFutureBlockUntilReady s_stub_fdbFutureBlockUntilReady;
		private static readonly FdbFutureGetErrorDelegate s_stub_fdbFutureGetError;
		private static readonly FdbFutureSetCallbackDelegate s_stub_fdbFutureSetCallback;

		private static readonly FdbCreateClusterDelegate s_stub_fdbCreateCluster;
		private static readonly FdbClusterDestroyDelegate s_stub_fdbClusterDestroy;
		private static readonly FdbClusterSetOptionDelegate s_stub_fdbClusterSetOption;
		private static readonly FdbFutureGetClusterDelegate s_stub_fdbFutureGetCluster;

		private static readonly FdbDatabaseDestroyDelegate s_stub_fdbDatabaseDestroy;
		private static readonly FdbClusterCreateDatabaseDelegate s_stub_fdbClusterCreateDatabase;
		private static readonly FdbFutureGetDatabaseDelegate s_stub_fdbFutureGetDatabase;

		private static readonly FdbDatabaseCreateTransactionDelegate s_stub_fdbDatabaseCreateTransaction;
		private static readonly FdbTransactionSetDelegate s_stub_fdbTransactionSet;
		private static readonly FdbTransactionCommitDelegate s_stub_fdbTransactionCommit;

		private static readonly Exception s_error;

		#endregion

		static FdbNativeStub()
		{
			try
			{
				s_lib = UnmanagedLibrary.LoadLibrary(
					Path.Combine(FdbCore.NativeLibPath, DLL_X86),
					Path.Combine(FdbCore.NativeLibPath, DLL_X64)
				);

				s_lib.Bind(ref s_stub_fdbGetError, "fdb_get_error");
				s_lib.Bind(ref s_stub_fdbSelectApiVersionImpl, "fdb_select_api_version_impl");
				s_lib.Bind(ref s_stub_fdbGetMaxApiVersion, "fdb_get_max_api_version");

				s_lib.Bind(ref s_stub_fdbNetworkSetOption, "fdb_network_set_option");
				s_lib.Bind(ref s_stub_fdbSetupNetwork, "fdb_setup_network");
				s_lib.Bind(ref s_stud_fdbRunNetwork, "fdb_run_network");
				s_lib.Bind(ref s_stub_fdbStopNetwork, "fdb_stop_network");

				s_lib.Bind(ref s_stub_fdbFutureDestroy, "fdb_future_destroy");
				s_lib.Bind(ref s_stub_fdbFutureIsError, "fdb_future_is_error");
				s_lib.Bind(ref s_stub_fdbFutureIsReady, "fdb_future_is_ready");
				s_lib.Bind(ref s_stub_fdbFutureBlockUntilReady, "fdb_future_block_until_ready");
				s_lib.Bind(ref s_stub_fdbFutureGetError, "fdb_future_get_error");
				s_lib.Bind(ref s_stub_fdbFutureSetCallback, "fdb_future_set_callback");

				s_lib.Bind(ref s_stub_fdbCreateCluster, "fdb_create_cluster");
				s_lib.Bind(ref s_stub_fdbClusterDestroy, "fdb_cluster_destroy");
				s_lib.Bind(ref s_stub_fdbClusterSetOption, "fdb_cluster_set_option");
				s_lib.Bind(ref s_stub_fdbFutureGetCluster, "fdb_future_get_cluster");

				s_lib.Bind(ref s_stub_fdbClusterCreateDatabase, "fdb_cluster_create_database");
				s_lib.Bind(ref s_stub_fdbDatabaseDestroy, "fdb_database_destroy");
				s_lib.Bind(ref s_stub_fdbFutureGetDatabase, "fdb_future_get_database");

				s_lib.Bind(ref s_stub_fdbDatabaseCreateTransaction, "fdb_database_create_transaction");
				s_lib.Bind(ref s_stub_fdbTransactionSet, "fdb_transaction_set");
				s_lib.Bind(ref s_stub_fdbTransactionCommit, "fdb_transaction_commit");

			}
			catch (Exception e)
			{
				if (s_lib != null) s_lib.Dispose();
				s_lib = null;
				s_error = e;
			}
		}

		public static bool IsLoaded
		{
			get { return s_error == null && s_lib != null; }
		}

		private static void EnsureLibraryIsLoaded()
		{
			// should be inlined
			if (s_error != null || s_lib == null) FailLibraryDidNotLoad();
		}

		private static void FailLibraryDidNotLoad()
		{
			throw new InvalidOperationException("An error occured while loading native FoundationDB library", s_error);
		}

		private static string ToManagedString(byte* nativeString)
		{
			if (nativeString == null) return null;
			return Marshal.PtrToStringAuto(new IntPtr((void*)nativeString));
		}

		private static string ToManagedString(IntPtr nativeString)
		{
			if (nativeString == IntPtr.Zero) return null;
			return Marshal.PtrToStringAuto(nativeString);
		}

		internal static byte[] ToNativeString(string value)
		{
			if (value == null) return null;
			// NULL terminated ANSI string
			byte[] result = new byte[value.Length + 1];
			// NULL at the end
			int p = 0;
			foreach (var c in value)
			{
				result[p++] = (byte)c;
			}
			return result;
		}

		#region API Basics..

		public static string GetError(FdbError code)
		{
			EnsureLibraryIsLoaded();
			return ToManagedString(s_stub_fdbGetError(code));
		}

		public static FdbError SelectApiVersionImpl(int runtimeVersion, int headerVersion)
		{
			EnsureLibraryIsLoaded();
			return s_stub_fdbSelectApiVersionImpl(runtimeVersion, headerVersion);
		}

		public static FdbError SelectApiVersion(int version)
		{
			return SelectApiVersionImpl(version, FDB_API_VERSION);
		}

		public static int GetMaxApiVersion()
		{
			EnsureLibraryIsLoaded();
			return s_stub_fdbGetMaxApiVersion();
		}

		#endregion

		#region Futures...

		public static bool FutureIsReady(FutureHandle futureHandle)
		{
			EnsureLibraryIsLoaded();
			return s_stub_fdbFutureIsReady(futureHandle);
		}

		public static void FutureDestroy(IntPtr futureHandle)
		{
			EnsureLibraryIsLoaded();
			s_stub_fdbFutureDestroy(futureHandle);
		}

		public static bool FutureIsError(FutureHandle futureHandle)
		{
			EnsureLibraryIsLoaded();
			return s_stub_fdbFutureIsError(futureHandle);
		}

		/// <summary>Return the error got from a FDBFuture</summary>
		/// <param name="futureHandle"></param>
		/// <returns></returns>
		public static FdbError FutureGetError(FutureHandle future)
		{
			EnsureLibraryIsLoaded();
			return s_stub_fdbFutureGetError(future, null);
		}

		public static FdbError FutureGetError(FutureHandle future, out string description)
		{
			EnsureLibraryIsLoaded();

			byte* ptr = null;
			var err = s_stub_fdbFutureGetError(future, &ptr);
			description = ToManagedString(ptr);
			return err;
		}

		public static FdbError FutureBlockUntilReady(FutureHandle future)
		{
			EnsureLibraryIsLoaded();

			return s_stub_fdbFutureBlockUntilReady(future);
		}

		public static FdbError FutureSetCallback(FutureHandle future, FdbFutureCallback callback, IntPtr callbackParameter)
		{
			EnsureLibraryIsLoaded();
			return s_stub_fdbFutureSetCallback(future, callback, callbackParameter);
		}

		#endregion

		#region Network...

		public static FdbError NetworkSetOption(FdbNetworkOption option, byte* value, int valueLength)
		{
			EnsureLibraryIsLoaded();
			return s_stub_fdbNetworkSetOption(option, value, valueLength);
		}

		public static FdbError SetupNetwork()
		{
			return s_stub_fdbSetupNetwork();
		}

		public static FdbError RunNetwork()
		{
			return s_stud_fdbRunNetwork();
		}

		public static FdbError StopNetwork()
		{
			return s_stub_fdbStopNetwork();
		}

		#endregion

		#region Clusters...

		public static FutureHandle CreateCluster(string path)
		{
			EnsureLibraryIsLoaded();

			var data = ToNativeString(path);
			fixed (byte* ptr = data)
			{
				var future = new FutureHandle();
				var handle = s_stub_fdbCreateCluster(ptr);
				future.TrySetHandle(handle);
				return future;
			}
		}

		public static void ClusterDestroy(IntPtr handle)
		{
			EnsureLibraryIsLoaded();
			RuntimeHelpers.PrepareConstrainedRegions();
			try { }
			finally
			{
				s_stub_fdbClusterDestroy(handle);
			}
		}

		public static FdbError FutureGetCluster(FutureHandle future, out ClusterHandle cluster)
		{
			EnsureLibraryIsLoaded();
			cluster = new ClusterHandle();
			FdbError err;

			RuntimeHelpers.PrepareConstrainedRegions();
			try { }
			finally
			{
				IntPtr handle;
				err = s_stub_fdbFutureGetCluster(future, out handle);
				//TODO: check is err == Success ?
				cluster.TrySetHandle(handle);
			}
			return err;
		}

		#endregion

		#region Databases...

		public static FdbError FutureGetDatabase(FutureHandle future, out DatabaseHandle database)
		{
			EnsureLibraryIsLoaded();

			database = new DatabaseHandle();
			FdbError err;

			RuntimeHelpers.PrepareConstrainedRegions();
			try { }
			finally
			{
				IntPtr handle;
				err = s_stub_fdbFutureGetDatabase(future.Handle, out handle);
				//TODO: check is err == Success ?
				database.TrySetHandle(handle);
			}
			return err;
		}

		public static void DatabaseDestroy(IntPtr handle)
		{
			EnsureLibraryIsLoaded();

			RuntimeHelpers.PrepareConstrainedRegions();
			try { }
			finally
			{
				s_stub_fdbDatabaseDestroy(handle);
			}
		}

		public static FutureHandle CreateClusterDatabase(ClusterHandle cluster, string name)
		{
			EnsureLibraryIsLoaded();

			var data = ToNativeString(name);
			fixed (byte* ptr = data)
			{
				var future = new FutureHandle();

				RuntimeHelpers.PrepareConstrainedRegions();
				try { }
				finally
				{
					var handle = s_stub_fdbClusterCreateDatabase(cluster, ptr, data == null ? 0 : data.Length);
					future.TrySetHandle(handle);
				}
				return future;
			}
		}

		#endregion

		#region Transactions...

		public static void TransactionDestroy(IntPtr handle)
		{
			EnsureLibraryIsLoaded();

			RuntimeHelpers.PrepareConstrainedRegions();
			try { }
			finally
			{
				s_stub_fdbDatabaseDestroy(handle);
			}
		}

		public static FdbError DatabaseCreateTransaction(DatabaseHandle database, out TransactionHandle transaction)
		{
			EnsureLibraryIsLoaded();
			transaction = new TransactionHandle();
			FdbError err;

			RuntimeHelpers.PrepareConstrainedRegions();
			try { }
			finally
			{
				IntPtr handle;
				err = s_stub_fdbDatabaseCreateTransaction(database, out handle);
				Debug.WriteLine("fdb_database_create_transaction(" + database.Handle.ToInt64().ToString("x") + ") => err=" + err + ", handle=" + handle.ToInt64().ToString("x"));
				transaction.TrySetHandle(handle);
			}
			return err;

		}

		public static void TransactionSet(TransactionHandle transaction, byte[] key, byte[] value)
		{
			EnsureLibraryIsLoaded();
			//TODO: nullcheck!
			fixed (byte* pKey = key)
			fixed (byte* pValue = value)
			{
				Debug.WriteLine("fdb_transaction_set(" + transaction.Handle.ToInt64().ToString("x") + ", [" + key.Length + "], [" + value.Length + "])");
				s_stub_fdbTransactionSet(transaction, pKey, key.Length, pValue, value.Length);
			}
		}

		public static FutureHandle TransactionCommit(TransactionHandle transaction)
		{
			EnsureLibraryIsLoaded();
			var future = new FutureHandle();

			RuntimeHelpers.PrepareConstrainedRegions();
			try { }
			finally
			{
				var handle = s_stub_fdbTransactionCommit(transaction);
				Debug.WriteLine("fdb_transaction_commit(" + transaction.Handle.ToInt64().ToString("x") + ") => " + handle.ToInt64().ToString("x"));
				future.TrySetHandle(handle);
			}
			return future;
		}

		#endregion

	}

}
