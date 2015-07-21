using System;
using System.Text;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;
using System.Diagnostics;

namespace Consul.Test
{
    /// <summary>
    /// Summary description for AssemblyTest
    /// </summary>
    [TestClass]
    public class AssemblyTest
    {
        [TestMethod]
        public void Assembly_IsStrongNamed()
        {
            Type type = typeof(Consul.Client);
            string name = type.Assembly.FullName.ToString();
            Trace.WriteLine(name);
            Assert.IsTrue(type.Assembly.FullName.Contains("PublicKeyToken"));
        }
    }
}
