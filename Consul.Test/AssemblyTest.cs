using System;
using System.Text;
using System.Collections.Generic;
using Xunit;
using System.Reflection;
using System.Diagnostics;

namespace Consul.Test
{
    public class AssemblyTest
    {
        [Fact]
        public void Assembly_IsStrongNamed()
        {

            Type type = typeof(Consul.ConsulClient);
            TypeInfo typeInfo = type.GetTypeInfo();
            string name = typeInfo.Assembly.FullName.ToString();
            Assert.True(typeInfo.Assembly.FullName.Contains("PublicKeyToken"));

        }
    }
}