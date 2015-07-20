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
            Type t = typeof(Consul.Client);
            string s = t.Assembly.FullName.ToString();
            Trace.WriteLine(s);
            Assert.IsTrue(t.Assembly.FullName.Contains("PublicKeyToken"));
        }
    }
}
