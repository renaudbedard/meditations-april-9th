using UnityEngine;
using System;
using static Unity.Mathematics.math;
using UnityEngine.Assertions;

public class Pool<T> where T : class, new()
{
	readonly Func<T> factoryMethod = () => new T();
	readonly Action<T> cleanupMethod;

	T[] elements;
	int freeIndex;

	public Pool(Func<T> factoryMethod = null, Action<T> cleanupMethod = null)
	{
		if (factoryMethod != null)
			this.factoryMethod = factoryMethod;
		this.cleanupMethod = cleanupMethod;
	}

	public int Capacity
	{
		get => elements?.Length ?? 0;
		set
		{
			Assert.IsTrue(value >= Capacity, "Capacity cannot be decreased.");

			int oldLength = Capacity;
			int newLength = max(oldLength, value);

			Array.Resize(ref elements, newLength);
			Debug.Log($"Grew pool of {typeof(T).Name} to {elements.Length}");

			for (int i = oldLength; i < elements.Length; i++)
				elements[i] = factoryMethod();
		}
	}

	void Grow()
	{
		Capacity = (int) ceil(max(Capacity, 1) * 1.5f);
	}
			
	public T Take()
	{
		if (freeIndex == Capacity)
			Grow();

		return elements[freeIndex++];
	}

	public void Return(T element)
	{
		var oldIndex = Array.IndexOf(elements, element, 0, freeIndex);
		freeIndex--;
		var oldHead = elements[freeIndex];
		elements[freeIndex] = element;
		elements[oldIndex] = oldHead;

		cleanupMethod?.Invoke(element);
	}
}
