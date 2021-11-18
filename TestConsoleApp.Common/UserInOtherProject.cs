using System;
using MapTo;

namespace TestConsoleApp.Common
{
    public class UserInOtherProject
    {
        [MapProperty(SourcePropertyName = "TestName")]
        public string Name { get; set; }
        public int Age { get; set; }
    }
}
