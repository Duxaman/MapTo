using System;
using MapTo;

namespace TestConsoleApp.Common
{
    public class UserInOtherProject
    {
        [MapProperty(SourcePropertyName = "TestName"/*, SourceTypeName = "TestConsoleApp.ViewModels.UserViewModel"*/)]
        public string Name { get; set; }
        public int Age { get; set; }
    }
}
