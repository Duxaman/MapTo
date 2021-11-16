using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TestConsoleApp.Common;

namespace TestConsoleApp.Data.Models
{

    public class UserInSameProject
    {
        //[MapProperty(SourcePropertyName = "TestName"/*, SourceTypeName = "TestConsoleApp.ViewModels.UserViewModel"*/)]
        public string TestName { get; set; }
        public int Age { get; set; }

    }
}