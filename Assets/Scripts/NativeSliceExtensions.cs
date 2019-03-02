using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

public static class NativeSliceExtensions
{
	// from : https://forum.unity.com/threads/allow-setting-mesh-arrays-with-nativearrays.536736/#post-3541836
	public static unsafe void CopyToFast<T>(
			this NativeSlice<T> nativeSlice,
			T[] array)
			where T : struct
	{
		if (array == null)
		{
			throw new NullReferenceException(nameof(array) + " is null");
		}
		int nativeArrayLength = nativeSlice.Length;
		if (array.Length < nativeArrayLength)
		{
			throw new IndexOutOfRangeException(
				nameof(array) + " is shorter than " + nameof(nativeSlice));
		}
		int byteLength = nativeSlice.Length * UnsafeUtility.SizeOf<T>();
		void* managedBuffer = UnsafeUtility.AddressOf(ref array[0]);
		void* nativeBuffer = nativeSlice.GetUnsafePtr();
		UnsafeUtility.MemCpy(managedBuffer, nativeBuffer, byteLength);
	}
}
