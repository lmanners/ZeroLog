﻿using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Formatting;
using JetBrains.Annotations;

namespace ZeroLog
{
    public delegate void UnmanagedFormatterDelegate<T>(ref T value, StringBuffer stringBuffer, StringView view)
        where T : unmanaged;

    internal static unsafe class UnmanagedCache
    {
        internal delegate void FormatterDelegate(StringBuffer stringBuffer, byte* valuePtr, StringView view);

        private static readonly ConcurrentDictionary<IntPtr, FormatterDelegate> _unmanagedStructs = new ConcurrentDictionary<IntPtr, FormatterDelegate>();

        internal static void Register([NotNull] Type unmanagedType)
        {
            if (unmanagedType == null)
                throw new ArgumentNullException(nameof(unmanagedType));

            if (!typeof(IStringFormattable).IsAssignableFrom(unmanagedType))
                throw new ArgumentException($"Not an {nameof(IStringFormattable)} type: {unmanagedType}");

            // Ideally we would explicitly check that unmanagedType is actually unmanaged
            // However, I'm not sure that's possible.

            var generic = _registerMethod.MakeGenericMethod(unmanagedType);
            generic.Invoke(null, null);
        }

        private static readonly MethodInfo _registerMethod = typeof(UnmanagedCache).GetMethod(nameof(Register), new Type[] { });

        public static void Register<T>(UnmanagedFormatterDelegate<T> formatter)
            where T : unmanaged
        {
            _unmanagedStructs.TryAdd(typeof(T).TypeHandle.Value, (b, vp, view) => FormatterGeneric(b, vp, view, formatter));
            _unmanagedStructs.TryAdd(typeof(T?).TypeHandle.Value, (b, vp, view) => FormatterGenericNullable(b, vp, view, formatter));
        }

        public static void Register<T>()
            where T : unmanaged, IStringFormattable
        {
            _unmanagedStructs.TryAdd(typeof(T).TypeHandle.Value, (b, vp, view) => FormatterGeneric(b, vp, view, ValueHelper<T>.Formatter));
            _unmanagedStructs.TryAdd(typeof(T?).TypeHandle.Value, (b, vp, view) => FormatterGenericNullable(b, vp, view, ValueHelper<T>.Formatter));
        }

        private static void FormatterGeneric<T>(StringBuffer stringBuffer, byte* valuePtr, StringView view, UnmanagedFormatterDelegate<T> typedFormatter)
            where T : unmanaged
        {
            ref var typedValueRef = ref Unsafe.AsRef<T>(valuePtr);
            typedFormatter(ref typedValueRef, stringBuffer, view);
        }

        private static void FormatterGenericNullable<T>(StringBuffer stringBuffer, byte* valuePtr, StringView view, UnmanagedFormatterDelegate<T> typedFormatter)
            where T : unmanaged
        {
            ref var typedValueRef = ref Unsafe.AsRef<T?>(valuePtr);
            if (typedValueRef != null)
            {
                var value = typedValueRef.GetValueOrDefault(); // This copies the value, but this is the slower execution path.
                typedFormatter(ref value, stringBuffer, view);
            }
            else
            {
                stringBuffer.Append(LogManager.Config.NullDisplayString);
            }
        }

        public static bool TryGetFormatter(IntPtr typeHandle, out FormatterDelegate formatter)
        {
            return _unmanagedStructs.TryGetValue(typeHandle, out formatter);
        }

        // The point of this class is to allow us to generate a direct call to a known
        // method on an unknown, unconstrained generic value type. Normally this would
        // be impossible; you'd have to cast the generic argument and introduce boxing.
        // Instead we pay a one-time startup cost to create a delegate that will forward
        // the parameter to the appropriate method in a strongly typed fashion.
        private static class ValueHelper<T>
            where T : unmanaged
        {
            public static readonly UnmanagedFormatterDelegate<T> Formatter = Prepare();

            private static UnmanagedFormatterDelegate<T> Prepare()
            {
                // we only use this class for value types that also implement IStringFormattable
                var type = typeof(T);
                if (!typeof(IStringFormattable).IsAssignableFrom(type))
                    return null;

                var result = typeof(ValueHelper<T>)
                             .GetTypeInfo()
                             .GetMethod(nameof(Assign), BindingFlags.Static | BindingFlags.NonPublic)?
                             .MakeGenericMethod(type)
                             .Invoke(null, null);
                return (UnmanagedFormatterDelegate<T>)result;
            }

            private static UnmanagedFormatterDelegate<U> Assign<U>()
                where U : unmanaged, IStringFormattable
            {
                return ValueHelper2.DoFormat<U>;
            }
        }

        private static class ValueHelper2
        {
            public static void DoFormat<T>(ref T input, StringBuffer buffer, StringView view)
                where T : unmanaged, IStringFormattable
            {
                input.Format(buffer, view);
            }
        }
    }
}
