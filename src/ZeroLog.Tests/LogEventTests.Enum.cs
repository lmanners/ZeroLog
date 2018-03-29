﻿using System.Globalization;
using NUnit.Framework;

namespace ZeroLog.Tests
{
    public partial class LogEventTests
    {
        [Test]
        public void should_append_enum()
        {
            LogManager.RegisterEnum(typeof(TestEnum));

            _logEvent.AppendEnum(TestEnum.Bar);
            _logEvent.WriteToStringBuffer(_output);

            Assert.AreEqual("Bar", _output.ToString());
        }

        [Test]
        public void should_append_enum_generic()
        {
            LogManager.RegisterEnum(typeof(TestEnum));

            _logEvent.AppendGeneric(TestEnum.Baz);
            _logEvent.WriteToStringBuffer(_output);

            Assert.AreEqual("Baz", _output.ToString());
        }

        [Test]
        public void should_append_unregistered_enum()
        {
            _logEvent.AppendEnum(UnregisteredEnum.Bar);
            _logEvent.WriteToStringBuffer(_output);

            Assert.AreEqual("1", _output.ToString());
        }

        [Test]
        public void should_append_unregistered_enum_negative()
        {
            _logEvent.AppendEnum(UnregisteredEnum.Neg);
            _logEvent.WriteToStringBuffer(_output);

            Assert.AreEqual("-1", _output.ToString());
        }

        [Test]
        public void should_append_unregistered_enum_large()
        {
            _logEvent.AppendEnum(UnregisteredEnumLarge.LargeValue);
            _logEvent.WriteToStringBuffer(_output);

            Assert.AreEqual(((ulong)UnregisteredEnumLarge.LargeValue).ToString(CultureInfo.InvariantCulture), _output.ToString());
        }

        private enum TestEnum
        {
            Foo,
            Bar,
            Baz
        }

        private enum UnregisteredEnum
        {
            Foo,
            Bar,
            Baz,
            Neg = -1
        }

        private enum UnregisteredEnumLarge : ulong
        {
            LargeValue = long.MaxValue + 42UL
        }
    }
}
